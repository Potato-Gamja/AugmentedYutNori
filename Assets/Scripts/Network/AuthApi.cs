using System.Threading.Tasks;

// 가입·로그인 REST 호출. 받은 결과의 저장은 PlayerIdentity가 맡는다.
//   구글·Firebase 익명 로그인: POST /auth/firebase { idToken } → { playerId, nickname, token, guest }
//   예전 에디터 테스트용 게스트: POST /players { nickname } → { playerId, nickname, guestToken }
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
    // 서버는 토큰을 검증해 같은 Firebase 계정이면 같은 playerId를 돌려준다
    public async Task<FirebaseLoginResponse> LoginWithFirebaseAsync(string firebaseIdToken)
    {
        FirebaseLoginResponse response = await api.Post<FirebaseLoginRequest, FirebaseLoginResponse>(
            "/auth/firebase", new FirebaseLoginRequest { idToken = firebaseIdToken });

        if (response == null || string.IsNullOrEmpty(response.token))
            throw new ApiException("POST", "/auth/firebase", 200, "응답에 token이 없습니다.", null);

        return response;
    }

    // 계정 닉네임 설정·변경 (로그인 토큰 필요). 닉네임 중복 허용. 서버 오류: 400 형식 오류, 401 토큰 무효
    public async Task<ChangeNicknameResponse> ChangeNicknameAsync(string nickname)
    {
        ChangeNicknameResponse response = await api.Patch<ChangeNicknameRequest, ChangeNicknameResponse>(
            "/players/me/nickname", new ChangeNicknameRequest { nickname = nickname });

        // 응답 본문이 비어 있어도 성공이면 보낸 닉네임으로 저장한다
        return response ?? new ChangeNicknameResponse { nickname = nickname };
    }

    // 회원 탈퇴 (로그인 토큰 필요). 서버가 계정 데이터와 Firebase 계정을 완전히 삭제한다 → 204
    // 서버 오류: 409 IN_ROOM(방·게임 참가 중), 401 토큰 무효, 404 이미 없는 계정
    public Task WithdrawAsync()
    {
        return api.Delete<object>("/players/me");
    }
}

// POST /players 요청/응답 (예전 에디터 테스트용 게스트)
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

// POST /auth/firebase 요청/응답
public class FirebaseLoginRequest
{
    public string idToken;
}

public class FirebaseLoginResponse
{
    public string playerId;
    public string nickname;
    public string token;
    public int? gold;   // 보유 골드. 서버 LoginResponse에 아직 없음 (없으면 null → 기기 캐시 유지)
    public bool guest;  // 게스트(Firebase 익명 로그인) 계정이면 true
}

// PATCH /players/me/nickname 요청/응답
public class ChangeNicknameRequest
{
    public string nickname;
}

public class ChangeNicknameResponse
{
    public string playerId;
    public string nickname;
}
