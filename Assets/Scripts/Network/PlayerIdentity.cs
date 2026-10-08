using System.Threading.Tasks;
using UnityEngine;

// 이 기기의 플레이어 정보(게스트 ID, 토큰, 닉네임).
// 처음이면 POST /players로 게스트 토큰을 받아 PlayerPrefs에 저장하고, 다음부터는 저장된 값을 쓴다.
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
        this.keyPrefix = keyPrefix ?? "";
        PlayerId = PlayerPrefs.GetString(Key("playerId"), "");
        Token = PlayerPrefs.GetString(Key("token"), "");
        Nickname = PlayerPrefs.GetString(Key("nickname"), "");
    }

    // 저장된 토큰이 있으면 그대로, 없으면 서버에서 게스트 토큰을 받는다. 끝나면 api.Token도 채워 둔다
    public async Task EnsureGuestAsync(ApiClient api, string nickname)
    {
        if (!HasToken)
        {
            GuestLoginResponse response = await api.Post<GuestLoginRequest, GuestLoginResponse>(
                "/players", new GuestLoginRequest { nickname = nickname });

            if (response == null || string.IsNullOrEmpty(response.guestToken))
                throw new ApiException("POST", "/players", 200, "응답에 guestToken이 없습니다.", null);

            Save(response.playerId, response.guestToken, string.IsNullOrEmpty(response.nickname) ? nickname : response.nickname);
        }

        api.Token = Token;
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

// POST /players 요청/응답. 서버 REST 명세가 정해지면 필드 이름을 맞출 것
public class GuestLoginRequest
{
    public string nickname;
}

public class GuestLoginResponse
{
    public string playerId;
    public string nickname;
    public string guestToken;
}
