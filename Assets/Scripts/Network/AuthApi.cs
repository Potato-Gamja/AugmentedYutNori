using System.Threading.Tasks;

// 가입·로그인 REST 호출. 받은 결과의 저장은 PlayerIdentity가 맡는다.
//   게스트:      POST /players         { nickname } → { playerId, nickname, guestToken }
//   구글 로그인: POST /auth/firebase   { idToken }  → { playerId, nickname, token }
// 서버 명세: docs/google-login-server-spec.md
public class AuthApi
{
    private readonly ApiClient api;

    public AuthApi(ApiClient api)
    {
        this.api = api;
    }

    public async Task<GuestLoginResponse> CreateGuestAsync(string nickname)
    {
        GuestLoginResponse response = await api.Post<GuestLoginRequest, GuestLoginResponse>(
            "/players", new GuestLoginRequest { nickname = nickname });

        if (response == null || string.IsNullOrEmpty(response.guestToken))
            throw new ApiException("POST", "/players", 200, "응답에 guestToken이 없습니다.", null);

        return response;
    }

    // Firebase ID 토큰을 서버에 보내 플레이어 토큰으로 바꾼다.
    // 서버는 토큰을 검증해 같은 구글 계정이면 같은 playerId를 돌려준다
    public async Task<FirebaseLoginResponse> LoginWithFirebaseAsync(string firebaseIdToken)
    {
        FirebaseLoginResponse response = await api.Post<FirebaseLoginRequest, FirebaseLoginResponse>(
            "/auth/firebase", new FirebaseLoginRequest { idToken = firebaseIdToken });

        if (response == null || string.IsNullOrEmpty(response.token))
            throw new ApiException("POST", "/auth/firebase", 200, "응답에 token이 없습니다.", null);

        return response;
    }
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

// POST /auth/firebase 요청/응답. 서버 구현 시 이 필드 이름에 맞출 것
public class FirebaseLoginRequest
{
    public string idToken;
}

public class FirebaseLoginResponse
{
    public string playerId;
    public string nickname;
    public string token;
}
