using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// 웹(WebGL)용 구글 로그인. 브라우저의 Firebase JS SDK로 팝업 로그인을 하고 Firebase ID 토큰을 받는다.
// JS 쪽 코드는 Assets/Plugins/WebGL/FirebaseGoogleAuth.jslib, 결과는 SendMessage로 이 오브젝트의 On... 메서드에 온다.
// 팝업 차단을 피하려고, 버튼을 누를 때(PointerDown) Arm을 걸어 두고 브라우저의 pointerup 이벤트 안에서 팝업을 연다.
public class WebGLGoogleSignIn : MonoBehaviour, IGoogleSignIn
{
    // JS가 SendMessage로 찾는 이름이라 씬에서 겹치지 않게 한다
    public const string ReceiverName = "FirebaseGoogleAuthReceiver";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void FirebaseGoogleAuth_Init(string configJson, string receiverName);
    [DllImport("__Internal")] private static extern void FirebaseGoogleAuth_ArmPopup();
#else
    private static void FirebaseGoogleAuth_Init(string configJson, string receiverName) =>
        throw new PlatformNotSupportedException("WebGL 빌드에서만 동작합니다.");
    private static void FirebaseGoogleAuth_ArmPopup() =>
        throw new PlatformNotSupportedException("WebGL 빌드에서만 동작합니다.");
#endif

    private FirebaseAuthSettings settings;
    private TaskCompletionSource<bool> initTcs;
    private TaskCompletionSource<FirebaseSignInResult> signInTcs;

    public bool StartOnPointerDown => true;

    public void Configure(FirebaseAuthSettings authSettings)
    {
        settings = authSettings;
    }

    public Task InitializeAsync()
    {
        if (initTcs != null) return initTcs.Task;

        if (settings == null || string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.AuthDomain))
            throw new GoogleSignInException("FirebaseAuthSettings에 apiKey와 authDomain을 입력해 주세요.");

        initTcs = new TaskCompletionSource<bool>();
        string config = JsonConvert.SerializeObject(new WebConfig
        {
            apiKey = settings.ApiKey,
            authDomain = settings.AuthDomain,
            projectId = settings.ProjectId,
            appId = settings.AppId,
        });
        FirebaseGoogleAuth_Init(config, gameObject.name);
        return initTcs.Task;
    }

    public Task<FirebaseSignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (signInTcs != null) return signInTcs.Task;

        signInTcs = new TaskCompletionSource<FirebaseSignInResult>();
        cancellationToken.Register(() => Fail(new GoogleSignInException("로그인을 취소했습니다. 다시 시도해 주세요.", cancelled: true)));
        FirebaseGoogleAuth_ArmPopup();
        return signInTcs.Task;
    }

    // ---- JS(SendMessage)에서 호출 ----

    private void OnFirebaseReady(string _)
    {
        initTcs?.TrySetResult(true);
    }

    private void OnFirebaseInitFailed(string message)
    {
        initTcs?.TrySetException(new GoogleSignInException($"Firebase 초기화 실패: {message}"));
    }

    private void OnGoogleSignInSucceeded(string json)
    {
        var user = JsonConvert.DeserializeObject<WebSignInResult>(json);
        var tcs = signInTcs;
        signInTcs = null;
        tcs?.TrySetResult(new FirebaseSignInResult
        {
            IdToken = user.idToken,
            Uid = user.uid,
            Email = user.email,
            DisplayName = user.displayName,
        });
    }

    private void OnGoogleSignInFailed(string json)
    {
        var error = JsonConvert.DeserializeObject<WebSignInError>(json);
        bool cancelled = error.code == "auth/popup-closed-by-user" || error.code == "auth/cancelled-popup-request";
        string message = error.code == "auth/popup-blocked"
            ? "브라우저가 로그인 팝업을 막았습니다. 팝업을 허용한 뒤 다시 눌러 주세요."
            : cancelled ? "구글 로그인을 취소했습니다." : $"구글 로그인 실패: {error.message}";

        Debug.LogWarning($"[GoogleSignIn] {error.code}: {error.message}");
        Fail(new GoogleSignInException(message, cancelled));
    }

    private void Fail(Exception exception)
    {
        var tcs = signInTcs;
        signInTcs = null;
        tcs?.TrySetException(exception);
    }

    private class WebConfig
    {
        public string apiKey;
        public string authDomain;
        public string projectId;
        public string appId;
    }

    private class WebSignInResult
    {
        public string idToken;
        public string uid;
        public string email;
        public string displayName;
    }

    private class WebSignInError
    {
        public string code;
        public string message;
    }
}
