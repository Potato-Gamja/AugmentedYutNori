using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 UI 요청을 REST/WebSocket 호출로 바꾸고 서버의 로비 메시지를 화면에 전달한다.
/// 네트워크 구현체의 세부 규칙은 이 클래스에만 모아 서버 계약 변경 범위를 제한한다.
/// </summary>
public class LobbyNetworkCoordinator : MonoBehaviour
{
    [SerializeField] private LobbyController lobbyController;
    [SerializeField] private WebSocketClient webSocketClient;
    [SerializeField] private string apiBaseUrl = "http://localhost:8080";
    [SerializeField] private string webSocketUrl = "ws://localhost:8080/ws/rooms/{roomCode}";
    [SerializeField] private string playerPrefsKeyPrefix = "";

    [Header("방 생성")]
    [SerializeField] private bool createRoomIsPublic = true;
    [SerializeField, Range(2, 4)] private int createRoomMaxPlayers = 4;
    [SerializeField, Min(1)] private int createRoomTimeLimitSec = 30;

    [Header("로그인")]
    [SerializeField] private string loginSceneName = "Login";
    [SerializeField] private string lobbySceneName = "Lobby";
    // 서버 로그인 연동 전·에디터 테스트에서는 게스트 생성을 유지한다.
    // 끄면 토큰이 없는 사용자를 로그인 씬으로 보낸다.
    [SerializeField] private bool allowGuestLogin = true;
    [SerializeField] private FirebaseAuthSettings authSettings;

    private static LobbyNetworkCoordinator instance;

    private ApiClient apiClient;
    private AuthApi authApi;
    private GachaApi gachaApi;
    private IconApi iconApi;
    private AchievementApi achievementApi;
    private PlayerIdentity playerIdentity;
    private GameReconnectCache reconnectCache;
    private bool reconnecting;
    private bool reconnectCanceled;
    private bool terminalReconnectFailure;
    private bool leavingRejectedGame;
    private string pendingReconnectFailure;
    private TaskCompletionSource<WsEnvelope> reconnectSnapshot;
    private GameReconnectData reconnectData;
    private string pendingRoomCode;
    private string pendingNickname;
    private bool awaitingRoomJoin;
    private bool changingRoomSocket;
    private int roomJoinVersion;
    private Task roomSocketCleanupTask = Task.CompletedTask;
    private const int MaxQuickJoinRetries = 2;
    private bool quickJoinInProgress;
    private int quickJoinVersion;
    private int quickJoinRetryCount;
    private bool leaveRequested;
    private bool gameExitInProgress;
    private readonly HashSet<string> reconnectingRoomPlayers = new();
    // Unity의 파괴된 객체는 ?. 검사에서 걸러지지 않으므로 실제 null로 바꿔서 호출한다.
    private LobbyController ActiveLobbyController => lobbyController != null ? lobbyController : null;
    // GAME_ENDED가 재전송돼도 보상을 한 번만 더하도록 (GAME_START마다 초기화)
    private bool rewardAppliedThisGame;
    private Task silentLoginTask = Task.CompletedTask;
    private bool silentLoginRunning;
    private bool recoveringFromUnauthorized;
    private int loginRedirectCount;   // GoToLogin 호출 횟수. 다시 로그인 도중 이미 로그인 화면으로 보냈는지 확인용
    // 서버 토큰(JWT) 만료가 이만큼 남으면 요청 전에 미리 새로 받는다 (게임 도중 만료 방지)
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromHours(1);
    private bool waitingForGameSession;
    private bool logoutRequested;
    private bool returnToRoomRequested;
    private bool gameEnded;
    private RoomStateMessage lastRoomState;
    private int roomVisibilityRequestVersion;
    private readonly List<WsEnvelope> pendingGameMessages = new();
    private readonly Dictionary<string, int> playerTeams = new();
    private readonly List<PlayerDisplayInfo> playerProfiles = new();

    public string LocalPlayerId => playerIdentity?.PlayerId ?? string.Empty;
    public string LocalNickname => playerIdentity?.Nickname ?? string.Empty;
    public int LocalGold => playerIdentity?.Gold ?? 0;
    public string LocalIconId { get; private set; } = string.Empty;
    public event Action LocalDisplayChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (webSocketClient == null) webSocketClient = GetComponent<WebSocketClient>();
        if (webSocketClient == null)
        {
            Debug.LogError("[LobbyNetwork] WebSocketClient가 없습니다.", this);
            enabled = false;
            return;
        }

        apiClient = new ApiClient(apiBaseUrl);
        authApi = new AuthApi(apiClient);
        gachaApi = new GachaApi(apiClient);
        iconApi = new IconApi(apiClient);
        achievementApi = new AchievementApi(apiClient);
        playerIdentity = new PlayerIdentity(playerPrefsKeyPrefix);
        reconnectCache = new GameReconnectCache(playerIdentity.ReconnectCacheKey);
        apiClient.Token = playerIdentity.Token;
        apiClient.Unauthorized += OnUnauthorized;
        webSocketClient.Url = webSocketUrl;

        // 게임 씬의 GameInstaller가 같은 WebSocket 연결로 RemoteGameSession을 만들 수 있게 등록한다.
        GameInstaller.RegisterRemoteSessionFactory((rules, events, state, turn, setup) =>
        {
            var session = new RemoteGameSession(webSocketClient, rules, events, state, turn, setup, pendingGameMessages);
            session.ReconnectRejected += OnGameReconnectRejected;
            pendingGameMessages.Clear();
            waitingForGameSession = false;
            return session;
        });

        webSocketClient.Connected += OnConnected;
        webSocketClient.Disconnected += OnDisconnected;
        webSocketClient.Error += OnSocketError;
        webSocketClient.MessageReceived += OnMessageReceived;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        BindLobbyController();
    }

    private void OnDestroy()
    {
        CancelQuickJoin();
        if (instance != this) return;

        CancelReconnect();
        UnbindLobbyController();
        if (apiClient != null) apiClient.Unauthorized -= OnUnauthorized;
        if (webSocketClient != null)
        {
            webSocketClient.Connected -= OnConnected;
            webSocketClient.Disconnected -= OnDisconnected;
            webSocketClient.Error -= OnSocketError;
            webSocketClient.MessageReceived -= OnMessageReceived;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 로그인 씬에서 저장한 토큰을 영속 코디네이터에도 반영한다.
        playerIdentity.Reload();
        apiClient.Token = playerIdentity.Token;
        BindLobbyController();
        RegisterGamePlayerTeams();
    }

    private void BindLobbyController()
    {
        LobbyController next = FindFirstObjectByType<LobbyController>();
        if (ReferenceEquals(next, lobbyController)) return;

        UnbindLobbyController();
        lobbyController = next;
        if (lobbyController == null) return;

        lobbyController.CreateRoomRequested += OnCreateRoomRequested;
        lobbyController.JoinRoomRequested += OnJoinRoomRequested;
        lobbyController.QuickJoinRequested += OnQuickJoinRequested;
        lobbyController.ReadyRequested += OnReadyRequested;
        lobbyController.StartGameRequested += OnStartGameRequested;
        lobbyController.LeaveRoomRequested += OnLeaveRoomRequested;

        if (!IsLoggedIn())
        {
            if (!allowGuestLogin) GoToLogin();
        }
        else
        {
            lobbyController.SetLocalPlayer(playerIdentity.PlayerId, playerIdentity.Nickname);
            if (silentLoginTask.IsCompleted && !PlayerIdentity.IsVerifiedThisRun(playerIdentity.Token))
                silentLoginTask = SilentLoginAsync();
        }
    }

    // Lobby 직접 실행, 토큰 만료 임박, 401을 받았을 때 저장된 Firebase 갱신 토큰으로 서버 토큰을 새로 받는다.
    // 새 토큰을 저장했으면 true
    private async Task<bool> SilentLoginAsync()
    {
        if (!CanSilentLogin()) return false;

        silentLoginRunning = true;
        try
        {
            FirebaseSignInResult firebaseUser = await new FirebaseTokenRefresher(authSettings)
                .RefreshAsync(playerIdentity.FirebaseRefreshToken);
            FirebaseLoginResponse login = await authApi.LoginWithFirebaseAsync(firebaseUser.IdToken);
            if (string.IsNullOrWhiteSpace(login.nickname) && login.playerId == playerIdentity.PlayerId)
                login.nickname = playerIdentity.Nickname;
            if (string.IsNullOrWhiteSpace(login.nickname))
            {
                GoToLogin();
                return false;
            }

            playerIdentity.ApplyLogin(login, firebaseUser.RefreshToken);
            apiClient.Token = playerIdentity.Token;
            ActiveLobbyController?.SetLocalPlayer(playerIdentity.PlayerId, playerIdentity.Nickname);
            return true;
        }
        catch (RefreshFailedException exception) when (exception.Expired)
        {
            playerIdentity.Clear();
            apiClient.Token = null;
            GoToLogin();
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LobbyNetwork] 자동 재로그인 실패, 저장된 로그인으로 계속합니다: {exception.Message}");
            return false;
        }
        finally
        {
            silentLoginRunning = false;
        }
    }

    private bool CanSilentLogin()
    {
        if (!playerIdentity.HasFirebaseLogin) return false;
        if (authSettings != null) return true;

        Debug.LogWarning("[LobbyNetwork] FirebaseAuthSettings가 없어 자동 재로그인을 건너뜁니다.", this);
        return false;
    }

    // 서버 토큰(JWT)이 곧 만료되면 요청을 보내기 전에 새로 받는다. 이미 다시 로그인 중이면 그걸 기다린다
    private async Task RefreshTokenIfExpiringAsync()
    {
        if (!JwtToken.IsExpiringWithin(playerIdentity.Token, TokenRefreshMargin)) return;

        if (silentLoginTask.IsCompleted) silentLoginTask = SilentLoginAsync();
        await silentLoginTask;
    }

    public async void OfferReconnect(LobbyController controller)
    {
        await silentLoginTask;
        if (this == null || controller == null || controller != lobbyController || !IsLoggedIn() || lastRoomState != null) return;
        if (!string.IsNullOrEmpty(pendingReconnectFailure))
        {
            string reason = pendingReconnectFailure;
            pendingReconnectFailure = null;
            controller.ShowReconnectFailure(reason);
            return;
        }
        GameReconnectData saved = reconnectCache.Load(LocalPlayerId, apiBaseUrl);
        if (saved != null) controller.ShowReconnect(saved.setup.roomCode);
    }

    public void CancelReconnect()
    {
        if (!reconnecting) return;
        reconnectCanceled = true;
        reconnectSnapshot?.TrySetCanceled();
    }

    public async Task ReconnectSavedGameAsync()
    {
        if (reconnecting) throw new InvalidOperationException("이미 재접속을 시도하고 있습니다.");
        reconnecting = true;
        reconnectCanceled = false;
        terminalReconnectFailure = false;
        reconnectSnapshot = new TaskCompletionSource<WsEnvelope>();
        try
        {
            await EnsurePlayer(LocalNickname);
            if (reconnectCanceled) throw new OperationCanceledException();
            reconnectData = reconnectCache.Load(LocalPlayerId, apiBaseUrl);
            if (reconnectData == null) throw new InvalidOperationException("이 기기에 복귀할 경기 정보가 없습니다.");
            await webSocketClient.CloseAndWait();
            if (reconnectCanceled) throw new OperationCanceledException();
            pendingRoomCode = reconnectData.setup.roomCode;
            leaveRequested = false;
            webSocketClient.Url = BuildRoomWebSocketUrl(pendingRoomCode);
            webSocketClient.Connect(playerIdentity.Token);
            Task completed = await Task.WhenAny(reconnectSnapshot.Task, Task.Delay(10000));
            if (completed != reconnectSnapshot.Task)
                throw new TimeoutException("서버 응답이 없습니다. 연결 상태를 확인한 뒤 다시 시도해 주세요.");
            await reconnectSnapshot.Task;
            if (this == null || lobbyController == null || reconnectCanceled) throw new OperationCanceledException();

            playerProfiles.Clear();
            playerProfiles.AddRange(reconnectData.profiles);
            playerTeams.Clear();
            foreach (PlayerDisplayInfo profile in playerProfiles)
                if (profile != null) playerTeams[profile.playerId] = profile.team;
            gameEnded = false;
            rewardAppliedThisGame = false;
            GameInstaller.PreparePlayerProfiles(playerProfiles);
            lobbyController.ReceiveGameStart(reconnectData.setup);
        }
        catch (Exception exception)
        {
            if (this != null)
            {
                await webSocketClient.CloseAndWait();
                pendingRoomCode = null;
                waitingForGameSession = false;
                pendingGameMessages.Clear();
                if (terminalReconnectFailure)
                {
                    ClearRoomSessionState();
                    reconnecting = false;
                    ActiveLobbyController?.ShowReconnectFailure(exception.Message);
                }
            }
            throw;
        }
        finally
        {
            reconnecting = false;
            reconnectCanceled = false;
            reconnectSnapshot = null;
            reconnectData = null;
        }
    }

    public Task WaitForLoginAsync() => silentLoginTask;

    private async Task EnsureAccountFeaturesAsync()
    {
        await EnsurePlayer(LocalNickname);
        // Firebase 계정(구글·게스트)은 서버 토큰(JWT)으로 모든 기능을 쓴다. 예전 에디터 테스트용 게스트 토큰만 서버 API가 받지 않는다
        if (!playerIdentity.IsGoogleLogin && !playerIdentity.IsGuest)
            throw new InvalidOperationException("이 계정으로는 이 기능을 사용할 수 없습니다. Google 계정 또는 게스트로 로그인해 주세요.");
    }


    public async Task<GachaResultResponse> DrawGachaAsync(int count = 1)
    {
        await EnsureAccountFeaturesAsync();
        GachaResultResponse response = await gachaApi.DrawAsync(count);
        playerIdentity.ApplyGold(response.gold);
        LocalDisplayChanged?.Invoke();
        return response;
    }

    public async Task<OwnedIconsResponse> GetOwnedIconsAsync()
    {
        await EnsureAccountFeaturesAsync();
        return await iconApi.GetOwnedAsync();
    }

    public async Task<AchievementListResponse> GetAchievementsAsync()
    {
        await EnsureAccountFeaturesAsync();
        return await achievementApi.GetAsync();
    }

    // 업적 보상 받기. 받은 뒤 보유 골드를 저장한다
    public async Task<ClaimAchievementResponse> ClaimAchievementAsync(string achievementId)
    {
        await EnsureAccountFeaturesAsync();
        ClaimAchievementResponse response = await achievementApi.ClaimAsync(achievementId);
        playerIdentity.ApplyGold(response.gold);
        LocalDisplayChanged?.Invoke();
        return response;
    }

    public async Task<PlayerProfileResponse> GetProfileAsync()
    {
        await EnsureAccountFeaturesAsync();
        PlayerProfileResponse response = await iconApi.GetProfileAsync();
        playerIdentity.ApplyGold(response.gold);
        if (!string.IsNullOrWhiteSpace(response.nickname))
            playerIdentity.ApplyNickname(response.nickname);
        ActiveLobbyController?.SetLocalPlayer(LocalPlayerId, LocalNickname);
        LocalIconId = response.iconId;
        LocalDisplayChanged?.Invoke();
        return response;
    }

    public async Task<PlayerProfileResponse> ChangeProfileIconAsync(string iconId)
    {
        OwnedIconsResponse owned = await GetOwnedIconsAsync();
        if (string.IsNullOrWhiteSpace(iconId) || !owned.iconIds.Contains(iconId))
            throw new InvalidOperationException("보유한 아이콘만 선택할 수 있습니다.");
        await iconApi.ChangeIconAsync(iconId);
        return await GetProfileAsync();
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null)
        {
            lobbyController = null;
            return;
        }
        lobbyController.CreateRoomRequested -= OnCreateRoomRequested;
        lobbyController.JoinRoomRequested -= OnJoinRoomRequested;
        lobbyController.QuickJoinRequested -= OnQuickJoinRequested;
        lobbyController.ReadyRequested -= OnReadyRequested;
        lobbyController.StartGameRequested -= OnStartGameRequested;
        lobbyController.LeaveRoomRequested -= OnLeaveRoomRequested;
        lobbyController = null;
    }

    private async void OnCreateRoomRequested(string nickname)
    {
        if (quickJoinInProgress) return;
        bool isPublic = lobbyController != null ? lobbyController.IsPublicRoom : createRoomIsPublic;
        try
        {
            await EnsurePlayer(nickname);

            // 서버 계약 확인 필요:
            // 현재는 설계표의 REST 방식인 POST /rooms를 사용한다.
            // 서버가 CREATE_ROOM WebSocket 방식으로 확정되면 이 호출만 메시지 전송으로 교체한다.
            CreateRoomRestResponse response = await apiClient.Post<CreateRoomRequest, CreateRoomRestResponse>(
                "/rooms", new CreateRoomRequest
                {
                    isPublic = isPublic,
                    maxPlayers = createRoomMaxPlayers,
                    timeLimit = createRoomTimeLimitSec,
                });

            if (response == null || string.IsNullOrWhiteSpace(response.code))
                throw new InvalidOperationException("방 생성 응답에 roomCode가 없습니다.");

            ConnectAndJoin(response.code, nickname);
        }
        catch (Exception exception)
        {
            ActiveLobbyController?.ReceiveError(ToUserMessage(exception));
        }
    }

    private async void OnQuickJoinRequested(string nickname)
    {
        if (quickJoinInProgress || awaitingRoomJoin || changingRoomSocket || logoutRequested || reconnecting) return;
        quickJoinInProgress = true;
        quickJoinRetryCount = 0;
        leaveRequested = false;
        int version = ++quickJoinVersion;
        try
        {
            await RequestQuickJoinAsync(nickname, version);
        }
        catch (Exception exception)
        {
            if (IsCurrentQuickJoin(version)) FailRoomJoin(ToUserMessage(exception));
        }
    }

    private bool IsCurrentQuickJoin(int version) => this != null && quickJoinInProgress &&
        version == quickJoinVersion && !logoutRequested && !leaveRequested;

    private async Task RequestQuickJoinAsync(string nickname, int version)
    {
        // 방 코드가 소켓 주소에 포함되므로 실패한 연결부터 닫고 새 방을 조회한다.
        await CloseRoomSocketForJoin();
        if (!IsCurrentQuickJoin(version)) return;
        await EnsurePlayer(nickname);
        if (!IsCurrentQuickJoin(version)) return;
        CreateRoomRestResponse response = await apiClient.Post<CreateRoomRequest, CreateRoomRestResponse>(
            "/rooms/quick-join", new CreateRoomRequest
            {
                isPublic = true,
                maxPlayers = createRoomMaxPlayers,
                timeLimit = createRoomTimeLimitSec,
            });
        if (!IsCurrentQuickJoin(version)) return;
        if (response == null || string.IsNullOrWhiteSpace(response.code))
            throw new InvalidOperationException("빠른 참가 응답에 방 코드가 없습니다.");
        ConnectAndJoin(response.code, playerIdentity.Nickname);
    }

    private async void RetryQuickJoin()
    {
        int version = quickJoinVersion;
        quickJoinRetryCount++;
        ++roomJoinVersion;
        awaitingRoomJoin = false;
        try { await RequestQuickJoinAsync(LocalNickname, version); }
        catch (Exception exception)
        {
            if (IsCurrentQuickJoin(version)) FailRoomJoin(ToUserMessage(exception));
        }
    }

    private void CancelQuickJoin()
    {
        quickJoinInProgress = false;
        quickJoinRetryCount = 0;
        ++quickJoinVersion;
    }

    private async void OnJoinRoomRequested(string nickname, string roomCode)
    {
        if (quickJoinInProgress) return;
        try
        {
            await EnsurePlayer(nickname);
            ConnectAndJoin(roomCode, nickname);
        }
        catch (Exception exception)
        {
            ActiveLobbyController?.ReceiveError(ToUserMessage(exception));
        }
    }

    // 로그인(또는 게스트 가입)된 플레이어가 있는지 확인한다
    private async System.Threading.Tasks.Task EnsurePlayer(string nickname)
    {
        await silentLoginTask;
        await RefreshTokenIfExpiringAsync();
        if (!IsLoggedIn())
        {
            if (!allowGuestLogin)
            {
                GoToLogin();
                throw new InvalidOperationException("로그인이 필요합니다.");
            }

            await playerIdentity.EnsureGuestAsync(authApi, nickname);
        }

        apiClient.Token = playerIdentity.Token;
        ActiveLobbyController?.SetLocalPlayer(playerIdentity.PlayerId, playerIdentity.Nickname);
    }

    public async Task<string> ChangeNicknameAsync(string nickname)
    {
        await silentLoginTask;
        if (!IsLoggedIn()) throw new InvalidOperationException("로그인이 필요합니다.");

        string requestedNickname = nickname?.Trim() ?? string.Empty;
        apiClient.Token = playerIdentity.Token;
        ChangeNicknameResponse response = await authApi.ChangeNicknameAsync(requestedNickname);
        string updatedNickname = string.IsNullOrWhiteSpace(response?.nickname)
            ? requestedNickname
            : response.nickname.Trim();

        playerIdentity.ApplyNickname(updatedNickname);
        ActiveLobbyController?.SetLocalPlayer(playerIdentity.PlayerId, updatedNickname);
        LocalDisplayChanged?.Invoke();
        return updatedNickname;
    }

    // 에디터 테스트용 게스트를 허용하지 않으면 Firebase 로그인(구글·게스트) 토큰만 로그인으로 친다
    // (예전에 만든 에디터 테스트용 게스트 토큰이 남아 있어도 로그인 씬으로 보낸다)
    private bool IsLoggedIn()
    {
        return playerIdentity.HasToken && (allowGuestLogin || playerIdentity.IsGoogleLogin || playerIdentity.IsGuest);
    }

    public async Task LogoutAsync()
    {
        if (logoutRequested) return;
        logoutRequested = true;
        await ClearSessionAndGoToLoginAsync();
    }

    // 회원 탈퇴: 서버가 계정 데이터와 Firebase 계정을 완전히 삭제한 뒤, 이 기기의 로그인 정보도 지우고 로그인 화면으로 간다.
    // 방·게임 참가 중(409 IN_ROOM) 등 서버가 거절하면 아무것도 지우지 않고 예외를 그대로 던진다
    public async Task WithdrawAsync()
    {
        if (logoutRequested) return;
        await EnsurePlayer(LocalNickname);
        await authApi.WithdrawAsync();
        logoutRequested = true;
        await ClearSessionAndGoToLoginAsync();
    }

    private async Task ClearSessionAndGoToLoginAsync()
    {
        CancelQuickJoin();
        try
        {
            // 진행 중인 자동 로그인이 로그아웃 후 토큰을 다시 저장하지 않도록 완료를 기다린다.
            await silentLoginTask;
            if (webSocketClient.IsConnected) webSocketClient.Send(MessageType.LEAVE_ROOM);
            await webSocketClient.CloseAndWait();
            reconnectCache?.Clear();
            playerIdentity.Clear();
            apiClient.Token = null;
            pendingRoomCode = null;
            pendingNickname = null;
            playerTeams.Clear();
            playerProfiles.Clear();
            pendingGameMessages.Clear();
            waitingForGameSession = false;
            SceneManager.LoadScene(loginSceneName);
            Destroy(gameObject);
        }
        catch
        {
            logoutRequested = false;
            throw;
        }
    }

    // 서버가 토큰을 거절(401: 만료·서버 키 변경 등)하면, 구글 로그인이면 먼저 조용히 새 토큰을 받아 본다.
    // 성공하면 로그인 화면으로 보내지 않는다 (실패했던 요청은 다시 시도하면 된다). 실패하면 로그인 화면으로
    private async void OnUnauthorized()
    {
        // 다시 로그인하는 요청 자체가 401이면 아래에서 또 다시 로그인하지 않는다 (SilentLoginAsync가 실패로 처리)
        if (silentLoginRunning || recoveringFromUnauthorized) return;

        if (CanSilentLogin())
        {
            int redirectsBefore = loginRedirectCount;
            recoveringFromUnauthorized = true;
            try
            {
                Task<bool> relogin = SilentLoginAsync();
                silentLoginTask = relogin;
                if (await relogin) return;
            }
            finally
            {
                recoveringFromUnauthorized = false;
            }
            // 갱신 토큰 만료, 서버에 닉네임 없음 → SilentLoginAsync가 이미 로그인 화면으로 보냄
            if (loginRedirectCount != redirectsBefore) return;
        }

        playerIdentity.Clear();
        apiClient.Token = null;
        GoToLogin();
    }

    private void GoToLogin()
    {
        CancelQuickJoin();
        ++loginRedirectCount;
        ++roomJoinVersion;
        awaitingRoomJoin = false;
        UnbindLobbyController();
        pendingRoomCode = null;
        pendingNickname = null;
        if (webSocketClient.IsConnected) webSocketClient.Close();

        if (SceneManager.GetActiveScene().name != loginSceneName)
            SceneManager.LoadScene(loginSceneName);
    }

    private async void ConnectAndJoin(string roomCode, string nickname)
    {
        string nextRoomCode = roomCode?.Trim();
        if (string.IsNullOrEmpty(nextRoomCode))
        {
            ActiveLobbyController?.ReceiveError("방 코드를 입력해 주세요.");
            return;
        }

        int version = ++roomJoinVersion;
        leaveRequested = false;
        try
        {
            // JOIN_ROOM은 소켓 주소의 방에만 입장한다. 이전 주소의 연결을 재사용하지 않는다.
            await CloseRoomSocketForJoin();
            if (this == null || version != roomJoinVersion || logoutRequested) return;
            pendingRoomCode = nextRoomCode;
            pendingNickname = nickname?.Trim();
            awaitingRoomJoin = true;
            webSocketClient.Url = BuildRoomWebSocketUrl(nextRoomCode);
            webSocketClient.Connect(playerIdentity.Token);
        }
        catch (Exception exception)
        {
            if (version == roomJoinVersion) FailRoomJoin(ToUserMessage(exception));
        }
    }

    private Task CloseRoomSocketForJoin()
    {
        if (!roomSocketCleanupTask.IsCompleted) return roomSocketCleanupTask;
        roomSocketCleanupTask = CloseRoomSocketForJoinCore();
        return roomSocketCleanupTask;
    }

    private async Task CloseRoomSocketForJoinCore()
    {
        changingRoomSocket = true;
        ClearRoomSessionState();
        try { if (webSocketClient != null) await webSocketClient.CloseAndWait(); }
        finally { changingRoomSocket = false; }
    }

    private async void FailRoomJoin(string message)
    {
        CancelQuickJoin();
        int version = ++roomJoinVersion;
        awaitingRoomJoin = false;
        try { await CloseRoomSocketForJoin(); }
        catch (Exception exception) { Debug.LogWarning($"[Lobby] 참가 실패 연결 정리: {exception.Message}"); }
        if (this != null && version == roomJoinVersion)
            ActiveLobbyController?.ReceiveError(message);
    }

    private string BuildRoomWebSocketUrl(string roomCode)
    {
        string escapedRoomCode = Uri.EscapeDataString(roomCode);
        if (webSocketUrl.Contains("{roomCode}"))
            return webSocketUrl.Replace("{roomCode}", escapedRoomCode);

        return $"{webSocketUrl.TrimEnd('/')}/rooms/{escapedRoomCode}";
    }

    private void OnConnected()
    {
        if (changingRoomSocket) return;
        // 재접속은 새 방 참가가 아니다. 진행 중인 방에 JOIN_ROOM을 보내지 않는다.
        if (reconnecting)
        {
            if (!webSocketClient.Send(MessageType.SYNC_REQUEST))
                reconnectSnapshot?.TrySetException(new InvalidOperationException("게임 상태를 요청하지 못했습니다."));
            return;
        }
        if (awaitingRoomJoin) SendJoinRoom();
    }

    private void SendJoinRoom()
    {
        if (string.IsNullOrEmpty(pendingRoomCode)) return;

        bool sent = webSocketClient.Send(MessageType.JOIN_ROOM, new JoinRoomRequest
        {
            roomCode = pendingRoomCode,
            nickname = pendingNickname,
        });

        if (!sent) FailRoomJoin("방 참가 요청을 보내지 못했습니다.");
    }

    private void OnReadyRequested(bool ready)
    {
        if (!CanReadyInCurrentRoom)
        {
            ActiveLobbyController?.ReceiveError("재경기는 서버의 방 초기화 기능 연결 후 가능합니다.");
            return;
        }
        if (!webSocketClient.IsConnected ||
            !webSocketClient.Send(MessageType.READY, new ReadyRequest { ready = ready }))
        {
            ActiveLobbyController?.ReceiveError("서버에 연결되어 있지 않습니다.");
        }
    }

    public bool CanStartGameInCurrentRoom => CanReadyInCurrentRoom && !leaveRequested &&
        lastRoomState?.status == "WAITING" && !string.IsNullOrEmpty(LocalPlayerId) &&
        lastRoomState.hostPlayerId == LocalPlayerId && lastRoomState.members != null &&
        lastRoomState.members.Count >= 2 && lastRoomState.members.Count <= 4 &&
        lastRoomState.members.Any(player => player != null && player.playerId == LocalPlayerId) &&
        lastRoomState.members.All(player => player != null && player.ready &&
            !reconnectingRoomPlayers.Contains(player.playerId));

    private void OnStartGameRequested()
    {
        if (!CanStartGameInCurrentRoom)
        {
            ActiveLobbyController?.ReceiveError("방장과 참가자의 준비·연결 상태를 다시 확인해 주세요.");
            return;
        }
        if (webSocketClient == null || !webSocketClient.IsConnected ||
            !webSocketClient.Send(MessageType.START_GAME))
        {
            ActiveLobbyController?.ReceiveError("서버에 연결되어 있지 않습니다.");
        }
    }

    private async void OnLeaveRoomRequested()
    {
        if (leaveRequested) return;
        CancelQuickJoin();
        ++roomJoinVersion;
        awaitingRoomJoin = false;
        leaveRequested = true;
        reconnectCache?.Clear();
        if (webSocketClient.IsConnected) webSocketClient.Send(MessageType.LEAVE_ROOM);
        // 이전 방 소켓이 닫힌 뒤 새 방 입장을 허용한다.
        await webSocketClient.CloseAndWait();
        // 이미 끊긴 소켓은 Disconnected를 발생시키지 않을 수 있다.
        if (leaveRequested)
        {
            ClearRoomSessionState();
            leaveRequested = false;
            ActiveLobbyController?.ReturnToLobby();
        }
    }

    public void ReturnToWaitingRoom()
    {
        if (gameExitInProgress || returnToRoomRequested) return;
        if (!webSocketClient.IsConnected || lastRoomState == null)
        {
            LeaveGameToMainMenu();
            return;
        }
        returnToRoomRequested = true;
        SceneManager.LoadScene(lobbySceneName);
    }

    // LobbyController.Start에서 패널 생성과 이벤트 연결이 끝난 뒤 호출한다.
    public void RestoreWaitingRoom(LobbyController controller)
    {
        if (!returnToRoomRequested || controller == null || lastRoomState == null) return;
        returnToRoomRequested = false;
        controller.SetLocalPlayer(LocalPlayerId, LocalNickname);
        controller.ReceiveRoomState(lastRoomState);
        controller.ReceiveRoomConnections(reconnectingRoomPlayers);
        _ = RefreshRoomVisibilityAsync(lastRoomState.roomCode);
    }

    private async void OnGameReconnectRejected(string reason)
    {
        if (leavingRejectedGame) return;
        leavingRejectedGame = true;
        reconnectCache?.Clear();
        // 이미 종료·제거된 게임이다. LEAVE_ROOM을 다시 보내지 않고 연결만 정리한다.
        try { if (webSocketClient != null) await webSocketClient.CloseAndWait(); }
        catch (Exception exception) { Debug.LogWarning($"[Reconnect] 연결 종료 실패: {exception.Message}"); }
        if (this == null) return;
        ClearRoomSessionState();
        pendingReconnectFailure = reason;
        SceneManager.LoadScene(lobbySceneName);
        leavingRejectedGame = false;
    }

    public bool CanReadyInCurrentRoom => !gameEnded;

    public async void LeaveGameToMainMenu()
    {
        if (gameExitInProgress) return;

        gameExitInProgress = true;
        leaveRequested = true;
        reconnectCache?.Clear();
        if (webSocketClient.IsConnected)
            webSocketClient.Send(MessageType.LEAVE_ROOM);
        try { if (webSocketClient != null) await webSocketClient.CloseAndWait(); }
        catch (Exception exception) { Debug.LogWarning($"[Lobby] 연결 종료 실패: {exception.Message}"); }
        if (this == null) return;
        ClearRoomSessionState();
        leaveRequested = false;
        SceneManager.LoadScene(lobbySceneName);
        gameExitInProgress = false;
    }

    private void OnMessageReceived(WsEnvelope envelope)
    {
        if (changingRoomSocket || (quickJoinInProgress && !awaitingRoomJoin)) return;
        // GAME_START 직후의 개인 선택지·확정 메시지가 씬 로딩 중 사라지지 않도록 보관한다.
        if (waitingForGameSession && envelope.type != MessageType.GAME_START &&
            envelope.type != MessageType.ROOM_STATE && envelope.type != MessageType.PONG)
            pendingGameMessages.Add(envelope);
        try
        {
            switch (envelope.type)
            {
                case MessageType.ROOM_STATE:
                    // LEAVE_ROOM 방송은 나간 본인에게도 올 수 있다. 대기실을 다시 열지 않는다.
                    if (leaveRequested) break;
                    RoomStateMessage room = MessageSerializer.ReadPayload<RoomStateMessage>(envelope);
                    if (awaitingRoomJoin)
                    {
                        if (room?.roomCode != pendingRoomCode ||
                            room.members?.Exists(member => member.playerId == LocalPlayerId) != true) break;
                        awaitingRoomJoin = false;
                        CancelQuickJoin();
                    }
                    if (lastRoomState?.roomCode != room?.roomCode) reconnectingRoomPlayers.Clear();
                    reconnectingRoomPlayers.RemoveWhere(id => room?.members == null || !room.members.Exists(member => member.playerId == id));
                    lastRoomState = room;
                    if (gameEnded && room?.status == "WAITING") gameEnded = false;
                    CachePlayerTeams(room);
                    ActiveLobbyController?.ReceiveRoomState(room);
                    ActiveLobbyController?.ReceiveRoomConnections(reconnectingRoomPlayers);
                    if (lobbyController != null && room != null)
                        _ = RefreshRoomVisibilityAsync(room.roomCode);
                    break;

                case MessageType.GAME_START:
                    GameStartMessage started = MessageSerializer.ReadPayload<GameStartMessage>(envelope);
                    if (started == null)
                        throw new InvalidOperationException("GAME_START 내용이 비어 있습니다.");
                    if (!playerTeams.TryGetValue(LocalPlayerId, out int localTeam) ||
                        localTeam < 0 || localTeam >= started.teamCount)
                        throw new InvalidOperationException("내 팀 정보를 찾을 수 없습니다. 대기방에 다시 참가해 주세요.");

                    // 서버 공통 GameSetup에는 localTeam이 없으므로 최신 ROOM_STATE의 배정을 사용한다.
                    started.localTeam = localTeam;
                    if (string.IsNullOrWhiteSpace(started.roomCode))
                        started.roomCode = pendingRoomCode ?? lastRoomState?.roomCode;
                    reconnectCache.Save(new GameReconnectData
                    {
                        playerId = LocalPlayerId,
                        serverUrl = apiBaseUrl,
                        setup = started,
                        profiles = new List<PlayerDisplayInfo>(playerProfiles),
                    });
                    gameEnded = false;
                    pendingGameMessages.Clear();
                    waitingForGameSession = true;
                    rewardAppliedThisGame = false;
                    GameInstaller.PreparePlayerProfiles(playerProfiles);
                    ActiveLobbyController?.ReceiveGameStart(started);
                    break;

                case MessageType.GAME_SNAPSHOT:
                    if (!reconnecting) break;
                    GameSnapshotMessage snapshot = MessageSerializer.ReadPayload<GameSnapshotMessage>(envelope);
                    if (!reconnectData.CanRestore(snapshot))
                    {
                        terminalReconnectFailure = true;
                        reconnectCache.Clear();
                        reconnectSnapshot.TrySetException(new InvalidOperationException("경기가 종료되었거나 이미 퇴장 처리되어 복귀할 수 없습니다."));
                        break;
                    }
                    if (!waitingForGameSession)
                    {
                        pendingGameMessages.Clear();
                        pendingGameMessages.Add(envelope);
                        waitingForGameSession = true;
                    }
                    reconnectSnapshot.TrySetResult(envelope);
                    break;

                case MessageType.PLAYER_DISCONNECTED:
                    PlayerDisconnectedMessage disconnected = MessageSerializer.ReadPayload<PlayerDisconnectedMessage>(envelope);
                    if (!leaveRequested && !string.IsNullOrEmpty(disconnected?.playerId) &&
                        lastRoomState?.members?.Exists(member => member.playerId == disconnected.playerId) == true)
                    {
                        reconnectingRoomPlayers.Add(disconnected.playerId);
                        ActiveLobbyController?.ReceiveRoomConnections(reconnectingRoomPlayers);
                    }
                    break;

                case MessageType.PLAYER_RECONNECTED:
                    PlayerReconnectedMessage reconnected = MessageSerializer.ReadPayload<PlayerReconnectedMessage>(envelope);
                    if (!leaveRequested && reconnected != null)
                    {
                        reconnectingRoomPlayers.Remove(reconnected.playerId);
                        ActiveLobbyController?.ReceiveRoomConnections(reconnectingRoomPlayers);
                    }
                    break;

                case MessageType.PLAYER_REMOVED:
                    PlayerRemovedMessage removed = MessageSerializer.ReadPayload<PlayerRemovedMessage>(envelope);
                    if (removed?.playerId == LocalPlayerId) reconnectCache?.Clear();
                    if (removed != null)
                    {
                        reconnectingRoomPlayers.Remove(removed.playerId);
                        ActiveLobbyController?.ReceiveRoomConnections(reconnectingRoomPlayers);
                    }
                    break;

                case MessageType.GAME_ENDED:
                    reconnectCache?.Clear();
                    if (reconnecting && !waitingForGameSession)
                    {
                        terminalReconnectFailure = true;
                        reconnectSnapshot?.TrySetException(new InvalidOperationException("경기가 종료되어 복귀할 수 없습니다."));
                        break;
                    }
                    gameEnded = true;
                    // 게임 화면 처리는 RemoteGameSession이 하고, 여기서는 내 보상만 보유 골드 캐시에 더한다
                    if (rewardAppliedThisGame) break;
                    GameEndedMessage ended = MessageSerializer.ReadPayload<GameEndedMessage>(envelope);
                    int reward = playerIdentity.ApplyGameReward(ended?.rankings);
                    rewardAppliedThisGame = true;
                    if (reward > 0) Debug.Log($"[LobbyNetwork] 게임 보상 {reward}골드, 보유 {playerIdentity.Gold}골드");
                    break;

                case MessageType.ERROR:
                    ErrorMessage error = MessageSerializer.ReadPayload<ErrorMessage>(envelope);
                    if (reconnecting)
                    {
                        if (ReconnectFailure.IsTerminal(error?.code))
                        {
                            terminalReconnectFailure = true;
                            reconnectCache.Clear();
                        }
                        reconnectSnapshot?.TrySetException(new InvalidOperationException(error?.message ?? "재접속 요청이 거절됐습니다."));
                        break;
                    }
                    if (waitingForGameSession && ReconnectFailure.IsTerminal(error?.code))
                    {
                        OnGameReconnectRejected(error?.message ?? "이 경기에 복귀할 수 없습니다.");
                        break;
                    }
                    if (awaitingRoomJoin)
                    {
                        if (quickJoinInProgress && error?.code == "ROOM_FULL" &&
                            quickJoinRetryCount < MaxQuickJoinRetries)
                            RetryQuickJoin();
                        else
                            FailRoomJoin(quickJoinInProgress && error?.code == "ROOM_FULL"
                                ? "참가 가능한 방에 입장하지 못했습니다. 잠시 후 다시 시도해 주세요."
                                : error?.message ?? "방 참가에 실패했습니다.");
                    }
                    else ActiveLobbyController?.ReceiveError(error?.message ?? "서버 요청이 거절됐습니다.");
                    break;
            }
        }
        catch (Exception exception)
        {
            if (reconnecting) reconnectSnapshot?.TrySetException(exception);
            else ActiveLobbyController?.ReceiveError($"서버 메시지를 처리하지 못했습니다: {exception.Message}");
        }
    }

    private async Task RefreshRoomVisibilityAsync(string roomCode)
    {
        int version = ++roomVisibilityRequestVersion;
        try
        {
            // ROOM_STATE에는 공개 여부가 없으므로 기존 방 조회 API를 사용한다.
            CreateRoomRestResponse response = await apiClient.Get<CreateRoomRestResponse>(
                $"/rooms/{Uri.EscapeDataString(roomCode)}");
            if (version != roomVisibilityRequestVersion || lastRoomState?.roomCode != roomCode) return;
            ActiveLobbyController?.ReceiveRoomVisibility(roomCode, response?.code == roomCode ? response.isPublic : null);
        }
        catch (Exception exception)
        {
            if (version != roomVisibilityRequestVersion || lastRoomState?.roomCode != roomCode) return;
            ActiveLobbyController?.ReceiveRoomVisibility(roomCode, null);
            Debug.LogWarning($"[LobbyNetwork] 방 공개 여부 조회 실패: {exception.Message}");
        }
    }

    private void OnSocketError(string message)
    {
        if (changingRoomSocket || (quickJoinInProgress && !awaitingRoomJoin)) return;
        if (reconnecting)
        {
            reconnectSnapshot?.TrySetException(new InvalidOperationException("서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요."));
            return;
        }
        if (awaitingRoomJoin) FailRoomJoin($"서버 연결 오류: {message}");
        else ActiveLobbyController?.ReceiveError($"서버 연결 오류: {message}");
    }

    private void OnDisconnected(bool byUser)
    {
        if (changingRoomSocket || (quickJoinInProgress && !awaitingRoomJoin)) return;
        if (reconnecting)
        {
            if (!byUser) reconnectSnapshot?.TrySetException(new InvalidOperationException("재접속 중 연결이 끊어졌습니다."));
            return;
        }
        if (awaitingRoomJoin)
        {
            FailRoomJoin("방 참가 중 서버 연결이 끊어졌습니다.");
            return;
        }
        pendingRoomCode = null;
        pendingNickname = null;
        if (logoutRequested) return;

        if (leaveRequested || byUser)
        {
            ClearRoomSessionState();
            leaveRequested = false;
            ActiveLobbyController?.ReturnToLobby();
            return;
        }

        ActiveLobbyController?.ReceiveError("서버 연결이 끊어졌습니다.");
    }

    private void ClearRoomSessionState()
    {
        awaitingRoomJoin = false;
        pendingRoomCode = null;
        pendingNickname = null;
        lastRoomState = null;
        reconnectingRoomPlayers.Clear();
        gameEnded = false;
        returnToRoomRequested = false;
        waitingForGameSession = false;
        rewardAppliedThisGame = false;
        pendingGameMessages.Clear();
        playerTeams.Clear();
        playerProfiles.Clear();
        roomVisibilityRequestVersion++;
    }

    private void CachePlayerTeams(RoomStateMessage room)
    {
        playerTeams.Clear();
        playerProfiles.Clear();
        if (room?.members == null) return;

        foreach (RoomPlayer player in room.members)
        {
            if (player == null || string.IsNullOrEmpty(player.playerId) || player.team < 0) continue;
            playerTeams[player.playerId] = player.team;
            playerProfiles.Add(new PlayerDisplayInfo
            {
                playerId = player.playerId,
                nickname = player.nickname,
                team = player.team,
                iconId = player.iconId,
            });
        }
    }

    private void RegisterGamePlayerTeams()
    {
        GameFlowController flow = FindFirstObjectByType<GameFlowController>();
        if (flow == null) return;

        foreach (KeyValuePair<string, int> playerTeam in playerTeams)
            flow.RegisterPlayerTeam(playerTeam.Key, playerTeam.Value);

        // 서버 담당 확인 사항:
        // 재접속으로 바로 게임 씬에 진입하는 경우 ROOM_STATE를 다시 받지 못할 수 있다.
        // 향후 GAME_START 또는 재접속 snapshot에도 playerId/team 목록을 포함하면
        // 같은 RegisterPlayerTeam 경로로 등록해야 한다.
    }

    private static string ToUserMessage(Exception exception)
    {
        return exception is ApiException apiException
            ? $"서버 요청에 실패했습니다. ({apiException.StatusCode})"
            : exception.Message;
    }
}

// 서버 계약 확인 필요:
// POST /rooms 응답이 중첩 객체이거나 필드명이 다르면 이 DTO만 서버 명세와 맞춘다.
public class CreateRoomRestResponse
{
    public string code;
    public bool? isPublic;
}
