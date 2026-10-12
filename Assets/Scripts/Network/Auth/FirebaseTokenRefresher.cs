using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// 저장된 Firebase 갱신 토큰으로 브라우저 없이 새 Firebase ID 토큰을 받는다 (Firebase Secure Token REST API).
// "이전 로그인으로 계속하기"에서 쓴다: 새 ID 토큰 → 서버 /auth/firebase → 새 게임 토큰.
// 서버를 재시작해 예전 게임 토큰이 무효가 돼도 항상 새 토큰으로 들어갈 수 있다. PC·웹 공통
public class FirebaseTokenRefresher
{
    private const string Endpoint = "https://securetoken.googleapis.com/v1/token";

    private readonly FirebaseAuthSettings settings;

    public FirebaseTokenRefresher(FirebaseAuthSettings settings)
    {
        this.settings = settings;
    }

    // 실패: 갱신 토큰 만료·취소(계정 비밀번호 변경, 계정 삭제 등) → Expired=true, 네트워크 문제 → Expired=false
    public async Task<FirebaseSignInResult> RefreshAsync(string refreshToken)
    {
        if (settings == null || string.IsNullOrEmpty(settings.ApiKey))
            throw new GoogleSignInException("FirebaseAuthSettings에 apiKey를 입력해 주세요.");
        if (string.IsNullOrEmpty(refreshToken))
            throw new RefreshFailedException("저장된 로그인이 없습니다.", expired: true);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        using var request = UnityWebRequest.Post($"{Endpoint}?key={settings.ApiKey}", form);
        request.timeout = 15;
        var done = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => done.TrySetResult(true);
        await done.Task;

        string text = request.downloadHandler?.text;
        if (request.result != UnityWebRequest.Result.Success)
        {
            // 400 = 갱신 토큰이 더 이상 유효하지 않음 (TOKEN_EXPIRED, INVALID_REFRESH_TOKEN, USER_DISABLED 등)
            bool expired = request.responseCode == 400;
            Debug.LogWarning($"[FirebaseTokenRefresher] 토큰 갱신 실패 ({request.responseCode}): {request.error}\n{text}");
            throw new RefreshFailedException(
                expired ? "저장된 로그인이 만료되었습니다. Google로 다시 로그인해 주세요." : "로그인 서버에 연결할 수 없습니다.",
                expired);
        }

        var response = JsonConvert.DeserializeObject<RefreshResponse>(text);
        if (string.IsNullOrEmpty(response?.id_token))
            throw new RefreshFailedException("토큰 갱신 응답에 id_token이 없습니다.", expired: false);

        return new FirebaseSignInResult
        {
            IdToken = response.id_token,
            // 응답에 새 갱신 토큰이 오면 그걸로 바꿔 저장한다
            RefreshToken = string.IsNullOrEmpty(response.refresh_token) ? refreshToken : response.refresh_token,
            Uid = response.user_id,
        };
    }

    // Secure Token API 응답 필드 이름 그대로
    private class RefreshResponse
    {
        public string id_token;
        public string refresh_token;
        public string user_id;
    }
}

public class RefreshFailedException : GoogleSignInException
{
    public bool Expired { get; }

    public RefreshFailedException(string message, bool expired) : base(message)
    {
        Expired = expired;
    }
}
