using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// 구글 계정으로 Firebase에 로그인하고 Firebase ID 토큰을 받아 온다.
// 플랫폼마다 방법이 달라 구현을 나눈다. 서버(POST /auth/firebase)에는 어느 쪽이든 같은 ID 토큰을 보낸다.
//   PC/에디터: DesktopGoogleSignIn (localhost 로그인 페이지 + Firebase JS SDK)
//   웹(WebGL): WebGLGoogleSignIn  (Firebase JS SDK 팝업)
public interface IGoogleSignIn
{
    // true면 버튼을 누르는 순간(PointerDown)에 SignInAsync를 불러야 한다.
    // 브라우저는 사용자 입력 이벤트 안에서 연 팝업만 허용하는데, Unity의 onClick은 그 이벤트가 끝난 뒤에 오기 때문
    bool StartOnPointerDown { get; }

    Task InitializeAsync();

    Task<FirebaseSignInResult> SignInAsync(CancellationToken cancellationToken = default);
}

// Firebase 로그인 결과. IdToken을 서버에 보내 플레이어 토큰으로 바꾼다
public class FirebaseSignInResult
{
    public string IdToken;
    // Firebase 갱신 토큰. 저장해 두면 다음에 브라우저 없이 새 IdToken을 받을 수 있다 (FirebaseTokenRefresher)
    public string RefreshToken;
    public string Uid;
    public string Email;
    public string DisplayName;
}

// 사용자가 취소했거나, 설정이 틀렸거나, 구글/Firebase가 거절한 경우
public class GoogleSignInException : Exception
{
    public bool Cancelled { get; }

    public GoogleSignInException(string message, bool cancelled = false) : base(message)
    {
        Cancelled = cancelled;
    }
}

public static class GoogleSignInFactory
{
    // receiverHost: WebGL에서 JS → C# 콜백(SendMessage)을 받을 GameObject를 붙일 곳
    public static IGoogleSignIn Create(FirebaseAuthSettings settings, GameObject receiverHost)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var receiver = new GameObject(WebGLGoogleSignIn.ReceiverName);
        receiver.transform.SetParent(receiverHost.transform, false);
        var signIn = receiver.AddComponent<WebGLGoogleSignIn>();
        signIn.Configure(settings);
        return signIn;
#else
        return new DesktopGoogleSignIn(settings);
#endif
    }
}
