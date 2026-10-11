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

    public bool HasToken => !string.IsNullOrEmpty(Token);

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
    }

    // 구글 로그인 결과 저장. 게스트 토큰과 같은 자리에 저장하므로 이후 흐름(WebSocket 접속 등)은 그대로다
    public void ApplyLogin(FirebaseLoginResponse response)
    {
        Save(response.playerId, response.token, response.nickname);
    }

    // 저장된 토큰이 있으면 그대로, 없으면 서버에서 게스트 토큰을 받는다. 에디터 테스트용
    public async Task EnsureGuestAsync(AuthApi auth, string nickname)
    {
        if (HasToken) return;

        GuestLoginResponse response = await auth.CreateGuestAsync(nickname);
        Save(response.playerId, response.guestToken, string.IsNullOrEmpty(response.nickname) ? nickname : response.nickname);
    }

    // 토큰이 만료됐거나(401) 다른 플레이어로 시작하고 싶을 때
    public void Clear()
    {
        PlayerPrefs.DeleteKey(Key("playerId"));
        PlayerPrefs.DeleteKey(Key("token"));
        PlayerPrefs.DeleteKey(Key("nickname"));
        PlayerPrefs.Save();
        PlayerId = Token = Nickname = "";
    }

    private void Save(string playerId, string token, string nickname)
    {
        PlayerId = playerId ?? "";
        Token = token ?? "";
        Nickname = nickname ?? "";
        PlayerPrefs.SetString(Key("playerId"), PlayerId);
        PlayerPrefs.SetString(Key("token"), Token);
        PlayerPrefs.SetString(Key("nickname"), Nickname);
        PlayerPrefs.Save();
    }

    private string Key(string name) => $"{keyPrefix}player.{name}";
}
