using System.Threading.Tasks;
using UnityEngine;

// 이 기기의 플레이어 정보(ID, 토큰, 닉네임).
// 로그인(구글) 또는 게스트 가입으로 받은 토큰을 PlayerPrefs에 저장하고, 다음부터는 저장된 값을 쓴다.
// REST 호출 자체는 AuthApi가 하고, 이 클래스는 결과를 저장·제공만 한다.
// playerId/token은 WebSocket 접속(WebSocketClient.Connect(token))과 JOIN_ROOM에서 사용자를 구분하는 데 쓴다.
public class PlayerIdentity
{
    // ParrelSync처럼 한 PC에서 두 클라이언트를 띄우면 PlayerPrefs를 같이 쓰므로, 접두어를 다르게 줘서 다른 플레이어로 만든다
    private readonly string keyPrefix;

    public string PlayerId { get; private set; }
    public string Token { get; private set; }
    public string Nickname { get; private set; }

    // 어떤 방법으로 받은 토큰인지 (authType)
    //   google      : 구글 로그인
    //   anonymous   : "게스트로 시작" (Firebase 익명 로그인). 구글처럼 갱신 토큰으로 다시 로그인한다
    //   guest       : 예전 에디터 테스트용 게스트 (POST /players). 구분이 없던 예전 저장값도 여기에 해당
    public bool IsGoogleLogin { get; private set; }
    public bool IsGuest { get; private set; }

    // Firebase 계정(구글 또는 게스트)으로 로그인했고 갱신 토큰이 있어서, 화면 없이 다시 로그인할 수 있는지
    public bool HasFirebaseLogin => (IsGoogleLogin || IsGuest) && !string.IsNullOrEmpty(FirebaseRefreshToken);

    // Firebase 로그인 때 받은 갱신 토큰. "이전 로그인으로 계속하기"에서 새 게임 토큰을 받을 때 쓴다
    public string FirebaseRefreshToken { get; private set; }

    // 보유 골드 캐시. 서버가 기준이고, 화면 표시용으로 이 기기에 들고 있는다
    //   로그인 응답에 gold가 오면 그 값으로 맞추고, 게임이 끝나면 GAME_ENDED의 내 rewardGold를 더한다
    public int Gold { get; private set; }

    internal string ReconnectCacheKey => keyPrefix + "ReconnectGame";

    public bool HasToken => !string.IsNullOrEmpty(Token);

    private static string verifiedTokenThisRun;

    public static bool IsVerifiedThisRun(string token) =>
        !string.IsNullOrEmpty(token) && token == verifiedTokenThisRun;

    // 도메인 리로드를 끈 에디터에서도 새 Play 시 서버 토큰을 다시 확인한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVerifiedToken() => verifiedTokenThisRun = null;

    public PlayerIdentity(string keyPrefix = "")
    {
        this.keyPrefix = (keyPrefix ?? "") + EditorInstancePrefix();
        Reload();
    }

    // Multiplayer Play Mode의 가상 플레이어는 Library/VP/mppmXXXX 폴더에서 실행되지만 PlayerPrefs는 메인 에디터와 같이 쓴다.
    // 그대로 두면 모든 창이 같은 토큰(같은 플레이어)이 되므로, 가상 플레이어는 폴더 이름을 접두어로 붙여 따로 저장한다.
    // 메인 에디터와 빌드는 접두어가 붙지 않는다
    private static string EditorInstancePrefix()
    {
#if UNITY_EDITOR
        string dataPath = Application.dataPath.Replace('\\', '/');
        int index = dataPath.IndexOf("/Library/VP/", System.StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            string rest = dataPath.Substring(index + "/Library/VP/".Length);
            int slash = rest.IndexOf('/');
            return (slash >= 0 ? rest.Substring(0, slash) : rest) + ".";
        }
#endif
        return "";
    }

    // 다른 PlayerIdentity(예: 로그인 씬)가 저장한 값을 다시 읽는다
    public void Reload()
    {
        PlayerId = PlayerPrefs.GetString(Key("playerId"), "");
        Token = PlayerPrefs.GetString(Key("token"), "");
        Nickname = PlayerPrefs.GetString(Key("nickname"), "");
        SetAuthType(PlayerPrefs.GetString(Key("authType"), ""));
        FirebaseRefreshToken = PlayerPrefs.GetString(Key("refreshToken"), "");
        Gold = PlayerPrefs.GetInt(Key("gold"), 0);
    }

    // Firebase 로그인(구글 또는 게스트) 결과 저장. 게스트 여부는 서버 응답(guest)을 따른다.
    // 다른 토큰과 같은 자리에 저장하므로 이후 흐름(WebSocket 접속 등)은 그대로다
    public void ApplyLogin(FirebaseLoginResponse response, string firebaseRefreshToken)
    {
        string previousPlayerId = PlayerId;
        Save(response.playerId, response.token, response.nickname, response.guest ? AuthTypeAnonymous : AuthTypeGoogle);
        verifiedTokenThisRun = Token;
        FirebaseRefreshToken = firebaseRefreshToken ?? "";
        PlayerPrefs.SetString(Key("refreshToken"), FirebaseRefreshToken);

        // 서버가 보유 골드를 알려 주면 그 값으로, 아니면 같은 계정일 때만 캐시를 유지한다
        if (response.gold.HasValue) SetGoldInternal(response.gold.Value);
        else if (previousPlayerId != PlayerId) SetGoldInternal(0);
        PlayerPrefs.Save();
    }

    // 게임 결과(GAME_ENDED rankings)에서 내 보상을 보유 골드에 더한다. 더한 금액을 돌려준다
    public int ApplyGameReward(RankingEntry[] rankings)
    {
        if (rankings == null || string.IsNullOrEmpty(PlayerId)) return 0;

        foreach (RankingEntry entry in rankings)
        {
            if (entry == null || entry.playerId != PlayerId) continue;
            if (entry.rewardGold <= 0) return 0;

            SetGoldInternal(Gold + entry.rewardGold);
            PlayerPrefs.Save();
            return entry.rewardGold;
        }
        return 0;
    }

    public void ApplyGold(int gold)
    {
        SetGoldInternal(gold);
        PlayerPrefs.Save();
    }

    private void SetGoldInternal(int gold)
    {
        Gold = Mathf.Max(0, gold);
        PlayerPrefs.SetInt(Key("gold"), Gold);
    }

    // 닉네임만 바꿔 저장 (닉네임 설정 화면에서 서버 저장이 끝난 뒤)
    public void ApplyNickname(string nickname)
    {
        Save(PlayerId, Token, nickname, CurrentAuthType());
    }

    // 저장된 토큰이 있으면 그대로, 없으면 서버에서 게스트 토큰을 받는다. 에디터 테스트용
    public async Task EnsureGuestAsync(AuthApi auth, string nickname)
    {
        if (HasToken) return;

        GuestLoginResponse response = await auth.CreateGuestAsync(nickname);
        Save(response.playerId, response.guestToken, string.IsNullOrEmpty(response.nickname) ? nickname : response.nickname, AuthTypeEditorGuest);
    }

    // 토큰이 만료됐거나(401) 다른 플레이어로 시작하고 싶을 때
    public void Clear()
    {
        verifiedTokenThisRun = null;
        PlayerPrefs.DeleteKey(Key("playerId"));
        PlayerPrefs.DeleteKey(Key("token"));
        PlayerPrefs.DeleteKey(Key("nickname"));
        PlayerPrefs.DeleteKey(Key("authType"));
        PlayerPrefs.DeleteKey(Key("refreshToken"));
        PlayerPrefs.DeleteKey(Key("gold"));
        PlayerPrefs.Save();
        PlayerId = Token = Nickname = FirebaseRefreshToken = "";
        Gold = 0;
        IsGoogleLogin = false;
        IsGuest = false;
    }

    private const string AuthTypeGoogle = "google";
    private const string AuthTypeAnonymous = "anonymous";
    private const string AuthTypeEditorGuest = "guest";   // 예전 값과 같게 유지 (저장된 값 호환)

    private string CurrentAuthType() =>
        IsGoogleLogin ? AuthTypeGoogle : IsGuest ? AuthTypeAnonymous : AuthTypeEditorGuest;

    private void SetAuthType(string authType)
    {
        IsGoogleLogin = authType == AuthTypeGoogle;
        IsGuest = authType == AuthTypeAnonymous;
    }

    private void Save(string playerId, string token, string nickname, string authType)
    {
        PlayerId = playerId ?? "";
        Token = token ?? "";
        Nickname = nickname ?? "";
        SetAuthType(authType);
        PlayerPrefs.SetString(Key("playerId"), PlayerId);
        PlayerPrefs.SetString(Key("token"), Token);
        PlayerPrefs.SetString(Key("nickname"), Nickname);
        PlayerPrefs.SetString(Key("authType"), authType);
        PlayerPrefs.Save();
    }

    private string Key(string name) => $"{keyPrefix}player.{name}";
}
