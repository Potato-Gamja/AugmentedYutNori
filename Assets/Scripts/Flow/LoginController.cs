using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로그인 화면 흐름. 구글(Firebase) 로그인 → 서버 POST /auth/firebase → 플레이어 토큰 저장 → 로비 씬으로 이동.
/// 토큰은 PlayerIdentity가 PlayerPrefs에 저장하므로 로비의 LobbyNetworkCoordinator가 같은 키 접두어로 그대로 이어 쓴다.
/// </summary>
public class LoginController : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private FirebaseAuthSettings authSettings;
    [SerializeField] private string apiBaseUrl = "http://localhost:8080";
    // LobbyNetworkCoordinator의 playerPrefsKeyPrefix와 같아야 로비에서 같은 플레이어로 이어진다
    [SerializeField] private string playerPrefsKeyPrefix = "";
    [SerializeField] private string nextSceneName = "Lobby";

    private LoginPanel loginPanel;
    private IGoogleSignIn googleSignIn;
    private AuthApi authApi;
    private PlayerIdentity playerIdentity;
    private bool signingIn;
    private CancellationTokenSource signInCancel;

    private async void Start()
    {
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        loginPanel = uiManager != null ? uiManager.Open<LoginPanel>() : null;
        if (loginPanel == null)
        {
            Debug.LogError("[Login] UIManager에 LoginPanel 프리팹을 등록해 주세요.", this);
            enabled = false;
            return;
        }

        loginPanel.GoogleLoginPressed += OnGoogleLoginPressed;
        loginPanel.GoogleLoginRequested += OnGoogleLoginRequested;
        loginPanel.CancelRequested += OnCancelRequested;

        authApi = new AuthApi(new ApiClient(apiBaseUrl));
        playerIdentity = new PlayerIdentity(playerPrefsKeyPrefix);

        loginPanel.SetBusy(true);
        loginPanel.ShowStatus("로그인을 준비하는 중입니다.");
        try
        {
            googleSignIn = GoogleSignInFactory.Create(authSettings, gameObject);
            await googleSignIn.InitializeAsync();
            if (this == null) return;

            loginPanel.SetBusy(false);
            loginPanel.ShowStatus(string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this != null) loginPanel.ShowStatus(ToUserMessage(exception));
        }
    }

    private void OnDestroy()
    {
        signInCancel?.Cancel();
        if (loginPanel == null) return;
        loginPanel.GoogleLoginPressed -= OnGoogleLoginPressed;
        loginPanel.GoogleLoginRequested -= OnGoogleLoginRequested;
        loginPanel.CancelRequested -= OnCancelRequested;
    }

    private void OnGoogleLoginPressed()
    {
        if (googleSignIn != null && googleSignIn.StartOnPointerDown) SignIn();
    }

    private void OnGoogleLoginRequested()
    {
        if (googleSignIn != null && !googleSignIn.StartOnPointerDown) SignIn();
    }

    // 브라우저 창을 그냥 닫으면 게임은 결과를 알 수 없으므로, 취소 버튼으로 기다림을 끝내고 다시 시도하게 한다
    private void OnCancelRequested()
    {
        signInCancel?.Cancel();
    }

    private async void SignIn()
    {
        if (signingIn) return;
        signingIn = true;
        signInCancel = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        loginPanel.SetBusy(true);
        loginPanel.SetCancelable(true);
        loginPanel.ShowStatus(googleSignIn.StartOnPointerDown
            ? "구글 로그인 창에서 계정을 선택해 주세요."
            : "열린 브라우저 창에서 'Google로 로그인'을 눌러 주세요.");

        try
        {
            FirebaseSignInResult firebaseUser = await googleSignIn.SignInAsync(signInCancel.Token);
            Debug.Log($"[Login] Firebase 로그인 성공: uid={firebaseUser.Uid}, email={firebaseUser.Email}");

            loginPanel.SetCancelable(false);
            loginPanel.ShowStatus("서버에 로그인하는 중입니다.");
            FirebaseLoginResponse login = await authApi.LoginWithFirebaseAsync(firebaseUser.IdToken);
            playerIdentity.ApplyLogin(login);
            Debug.Log($"[Login] 서버 로그인 성공: playerId={playerIdentity.PlayerId}");

            SceneManager.LoadScene(nextSceneName);
        }
        catch (Exception exception)
        {
            if (this == null) return;
            if (!(exception is GoogleSignInException { Cancelled: true })) Debug.LogException(exception, this);
            loginPanel.ShowStatus(ToUserMessage(exception));
            loginPanel.SetCancelable(false);
            loginPanel.SetBusy(false);
        }
        finally
        {
            signInCancel?.Dispose();
            signInCancel = null;
            signingIn = false;
        }
    }

    private static string ToUserMessage(Exception exception)
    {
        if (exception is ApiException apiException)
            return apiException.StatusCode == 0
                ? "서버에 연결할 수 없습니다. 서버가 켜져 있는지 확인해 주세요."
                : $"서버 로그인에 실패했습니다. ({apiException.StatusCode})";

        return exception.Message;
    }
}
