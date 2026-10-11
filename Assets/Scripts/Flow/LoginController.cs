using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로그인 화면 흐름. 구글(Firebase) 로그인 → 서버 POST /auth/firebase → 플레이어 토큰 저장
/// → (계정 닉네임이 없으면) 닉네임 설정 → 로비 씬으로 이동.
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
    private NicknamePanel nicknamePanel;
    private ApiClient apiClient;
    private IGoogleSignIn googleSignIn;
    private AuthApi authApi;
    private FirebaseTokenRefresher tokenRefresher;
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
        loginPanel.ContinueRequested += OnContinueRequested;

        apiClient = new ApiClient(apiBaseUrl);
        authApi = new AuthApi(apiClient);
        tokenRefresher = new FirebaseTokenRefresher(authSettings);
        playerIdentity = new PlayerIdentity(playerPrefsKeyPrefix);
        loginPanel.SetContinueAvailable(HasSavedLogin(), playerIdentity.Nickname);

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
        if (nicknamePanel != null) nicknamePanel.Submitted -= OnNicknameSubmitted;
        if (loginPanel == null) return;
        loginPanel.GoogleLoginPressed -= OnGoogleLoginPressed;
        loginPanel.GoogleLoginRequested -= OnGoogleLoginRequested;
        loginPanel.CancelRequested -= OnCancelRequested;
        loginPanel.ContinueRequested -= OnContinueRequested;
    }

    private void OnGoogleLoginPressed()
    {
        if (googleSignIn != null && googleSignIn.StartOnPointerDown) SignIn();
    }

    private void OnGoogleLoginRequested()
    {
        if (googleSignIn != null && !googleSignIn.StartOnPointerDown) SignIn();
    }

    // 이 기기에 저장된 구글 로그인이 있는지 (게스트 토큰은 제외). 갱신 토큰이 있어야 다시 로그인할 수 있다
    private bool HasSavedLogin() =>
        playerIdentity.IsGoogleLogin && !string.IsNullOrEmpty(playerIdentity.FirebaseRefreshToken);

    // 저장된 Firebase 갱신 토큰으로 브라우저 없이 다시 로그인한다.
    // 새 Firebase ID 토큰 → 서버 /auth/firebase → 새 게임 토큰이라, 서버를 재시작해 예전 게임 토큰이 무효가 돼도 들어갈 수 있다
    private async void OnContinueRequested()
    {
        if (signingIn || !HasSavedLogin()) return;
        signingIn = true;
        loginPanel.SetBusy(true);
        loginPanel.ShowStatus("이전 계정으로 로그인하는 중입니다.");

        try
        {
            FirebaseSignInResult firebaseUser = await tokenRefresher.RefreshAsync(playerIdentity.FirebaseRefreshToken);
            if (this == null) return;
            Debug.Log($"[Login] 저장된 로그인으로 Firebase 토큰 갱신: uid={firebaseUser.Uid}");

            await CompleteServerLogin(firebaseUser);
        }
        catch (Exception exception)
        {
            if (this == null) return;
            if (exception is RefreshFailedException { Expired: true })
            {
                // 갱신 토큰이 더 이상 유효하지 않음 → 저장된 로그인을 지우고 구글 로그인을 다시 하게 한다
                playerIdentity.Clear();
                loginPanel.SetContinueAvailable(false, null);
            }
            else
            {
                Debug.LogException(exception, this);
            }
            loginPanel.ShowStatus(ToUserMessage(exception));
            loginPanel.SetBusy(false);
        }
        finally
        {
            signingIn = false;
        }
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
            await CompleteServerLogin(firebaseUser);
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

    // Firebase ID 토큰을 서버 게임 토큰으로 바꿔 저장하고, 닉네임이 없으면 닉네임부터 정한 뒤 로비로 간다
    private async System.Threading.Tasks.Task CompleteServerLogin(FirebaseSignInResult firebaseUser)
    {
        loginPanel.ShowStatus("서버에 로그인하는 중입니다.");
        FirebaseLoginResponse login = await authApi.LoginWithFirebaseAsync(firebaseUser.IdToken);
        KeepLocalNicknameIfServerHasNone(login);
        playerIdentity.ApplyLogin(login, firebaseUser.RefreshToken);
        apiClient.Token = playerIdentity.Token;
        Debug.Log($"[Login] 서버 로그인 성공: playerId={playerIdentity.PlayerId}, nickname={playerIdentity.Nickname}");

        if (string.IsNullOrWhiteSpace(playerIdentity.Nickname))
            OpenNicknamePanel();
        else
            SceneManager.LoadScene(nextSceneName);
    }

    // 서버 닉네임 API가 생기기 전에는 닉네임을 이 기기에만 저장하므로, 서버가 빈 닉네임을 주면 같은 계정의 저장값을 유지한다
    private void KeepLocalNicknameIfServerHasNone(FirebaseLoginResponse login)
    {
        if (string.IsNullOrWhiteSpace(login.nickname) &&
            login.playerId == playerIdentity.PlayerId &&
            !string.IsNullOrWhiteSpace(playerIdentity.Nickname))
        {
            login.nickname = playerIdentity.Nickname;
        }
    }

    // 처음 로그인했거나 닉네임이 없는 계정이면 닉네임을 정한 뒤 로비로 간다
    private void OpenNicknamePanel()
    {
        nicknamePanel = uiManager.Get<NicknamePanel>();
        if (nicknamePanel == null)
        {
            Debug.LogError("[Login] UIManager에 NicknamePanel 프리팹을 등록해 주세요. 닉네임 없이 로비로 이동합니다.", this);
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        nicknamePanel.Submitted -= OnNicknameSubmitted;
        nicknamePanel.Submitted += OnNicknameSubmitted;
        uiManager.Close<LoginPanel>();
        uiManager.Open<NicknamePanel>();
    }

    private async void OnNicknameSubmitted(string nickname)
    {
        nicknamePanel.SetBusy(true);
        nicknamePanel.ShowStatus("닉네임을 저장하는 중입니다.");

        try
        {
            ChangeNicknameResponse response = await authApi.ChangeNicknameAsync(nickname);
            playerIdentity.ApplyNickname(string.IsNullOrWhiteSpace(response.nickname) ? nickname : response.nickname);
        }
        catch (ApiException exception) when (exception.StatusCode == 404 || exception.StatusCode == 405)
        {
            // 서버에 닉네임 API가 아직 없음 → 이 기기에만 저장하고 진행 (서버 구현 후 이 분기는 지울 것)
            Debug.LogWarning($"[Login] 서버 닉네임 API가 없어 이 기기에만 저장합니다. ({exception.Message})", this);
            playerIdentity.ApplyNickname(nickname);
        }
        catch (Exception exception)
        {
            if (this == null) return;
            nicknamePanel.ShowStatus(ToNicknameMessage(exception));
            nicknamePanel.SetBusy(false);
            return;
        }

        if (this == null) return;
        Debug.Log($"[Login] 닉네임 설정: {playerIdentity.Nickname}");
        SceneManager.LoadScene(nextSceneName);
    }

    private static string ToNicknameMessage(Exception exception)
    {
        if (exception is ApiException apiException)
        {
            switch (apiException.StatusCode)
            {
                case 0: return "서버에 연결할 수 없습니다. 서버가 켜져 있는지 확인해 주세요.";
                case 400: return "사용할 수 없는 닉네임입니다.";
                case 409: return "이미 사용 중인 닉네임입니다.";
                case 401: return "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            }
            return $"닉네임 저장에 실패했습니다. ({apiException.StatusCode})";
        }
        return exception.Message;
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
