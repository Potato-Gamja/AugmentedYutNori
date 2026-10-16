using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// "게스트로 시작": Firebase 익명 계정을 새로 만들고 ID 토큰을 받는다 (Firebase Auth REST accounts:signUp).
// 브라우저나 jslib 없이 UnityWebRequest만 써서 PC·웹 공통이다 (FirebaseTokenRefresher와 같은 방식).
// 받은 ID 토큰은 구글 로그인과 똑같이 서버 POST /auth/firebase로 보내고, 서버가 익명 로그인을 보고 게스트 계정으로 만든다.
// 갱신 토큰을 저장해 두면 다음에는 새 계정을 만들지 않고 FirebaseTokenRefresher로 같은 게스트 계정에 다시 들어간다.
// Firebase 콘솔 → Authentication → 로그인 방법에서 '익명'을 사용 설정해야 한다
public class FirebaseAnonymousSignIn
{
    private const string Endpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signUp";

    private readonly FirebaseAuthSettings settings;

    public FirebaseAnonymousSignIn(FirebaseAuthSettings settings)
    {
        this.settings = settings;
    }

    public async Task<FirebaseSignInResult> SignUpAsync()
    {
        if (settings == null || string.IsNullOrEmpty(settings.ApiKey))
            throw new GoogleSignInException("FirebaseAuthSettings에 apiKey를 입력해 주세요.");

        // 이메일·비밀번호 없이 보내면 익명 계정이 만들어진다
        byte[] body = Encoding.UTF8.GetBytes("{\"returnSecureToken\":true}");
        using var request = new UnityWebRequest($"{Endpoint}?key={settings.ApiKey}", UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 15,
        };
        request.SetRequestHeader("Content-Type", "application/json");

        var done = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => done.TrySetResult(true);
        await done.Task;

        string text = request.downloadHandler?.text;
        if (request.result != UnityWebRequest.Result.Success)
        {
            // 400 ADMIN_ONLY_OPERATION / OPERATION_NOT_ALLOWED = 콘솔에서 익명 로그인이 꺼져 있음
            Debug.LogWarning($"[FirebaseAnonymousSignIn] 게스트 계정 생성 실패 ({request.responseCode}): {request.error}\n{text}");
            throw new GoogleSignInException(request.responseCode == 400
                ? "게스트로 시작할 수 없습니다. (Firebase 익명 로그인 설정 확인 필요)"
                : "로그인 서버에 연결할 수 없습니다.");
        }

        var response = JsonConvert.DeserializeObject<SignUpResponse>(text);
        if (string.IsNullOrEmpty(response?.idToken) || string.IsNullOrEmpty(response.refreshToken))
            throw new GoogleSignInException("게스트 계정 생성 응답이 올바르지 않습니다.");

        return new FirebaseSignInResult
        {
            IdToken = response.idToken,
            RefreshToken = response.refreshToken,
            Uid = response.localId,
        };
    }

    // accounts:signUp 응답 필드 이름 그대로
    private class SignUpResponse
    {
        public string idToken;
        public string refreshToken;
        public string localId;
    }
}
