using System;
using System.Collections.Generic;
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
    private PlayerIdentity playerIdentity;
    private string pendingRoomCode;
    private string pendingNickname;
    private bool leaveRequested;
    // GAME_ENDED가 재전송돼도 보상을 한 번만 더하도록 (GAME_START마다 초기화)
    private bool rewardAppliedThisGame;
    private Task silentLoginTask = Task.CompletedTask;
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
        playerIdentity = new PlayerIdentity(playerPrefsKeyPrefix);
        apiClient.Token = playerIdentity.Token;
        apiClient.Unauthorized += OnUnauthorized;
        webSocketClient.Url = webSocketUrl;

        // 게임 씬의 GameInstaller가 같은 WebSocket 연결로 RemoteGameSession을 만들 수 있게 등록한다.
        GameInstaller.RegisterRemoteSessionFactory((rules, events, state, turn, setup) =>
        {
            var session = new RemoteGameSession(webSocketClient, rules, events, state, turn, setup, pendingGameMessages);
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
        if (instance != this) return;

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
        if (next == lobbyController) return;

        UnbindLobbyController();
        lobbyController = next;
        if (lobbyController == null) return;

        lobbyController.CreateRoomRequested += OnCreateRoomRequested;
        lobbyController.JoinRoomRequested += OnJoinRoomRequested;
        lobbyController.QuickJoinRequested += OnQuickJoinRequested;
        lobbyController.ReadyRequested += OnReadyRequested;
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

    // Lobby 직접 실행 시 저장된 Firebase 갱신 토큰으로 서버 토큰을 새로 받는다.
    private async Task SilentLoginAsync()
    {
        if (!playerIdentity.IsGoogleLogin || string.IsNullOrEmpty(playerIdentity.FirebaseRefreshToken)) return;
        if (authSettings == null)
        {
            Debug.LogWarning("[LobbyNetwork] FirebaseAuthSettings가 없어 자동 재로그인을 건너뜁니다.", this);
            return;
        }

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
                return;
            }

            playerIdentity.ApplyLogin(login, firebaseUser.RefreshToken);
            apiClient.Token = playerIdentity.Token;
            lobbyController?.SetLocalPlayer(playerIdentity.PlayerId, playerIdentity.Nickname);
        }
        catch (RefreshFailedException exception) when (exception.Expired)
        {
            playerIdentity.Clear();
            apiClient.Token = null;
            GoToLogin();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LobbyNetwork] 자동 재로그인 실패, 저장된 로그인으로 계속합니다: {exception.Message}");
        }
    }

    public Task WaitForLoginAsync() => silentLoginTask;

    public async Task<GachaResultResponse> DrawGachaAsync()
    {
        await EnsurePlayer(LocalNickname);
        GachaResultResponse response = await gachaApi.DrawAsync();
        playerIdentity.ApplyGold(response.gold);
        return response;
    }

    public async Task<OwnedIconsResponse> GetOwnedIconsAsync()
    {
        await EnsurePlayer(LocalNickname);
        return await iconApi.GetOwnedAsync();
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null) return;
        lobbyController.CreateRoomRequested -= OnCreateRoomRequested;
        lobbyController.JoinRoomRequested -= OnJoinRoomRequested;
        lobbyController.QuickJoinRequested -= OnQuickJoinRequested;
        lobbyController.ReadyRequested -= OnReadyRequested;
        lobbyController.LeaveRoomRequested -= OnLeaveRoomRequested;
        lobbyController = null;
    }

    private async void OnCreateRoomRequested(string nickname)
    {
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
            lobbyController?.ReceiveError(ToUserMessage(exception));
        }
    }

    private async void OnQuickJoinRequested(string nickname)
    {
        try
        {
            await EnsurePlayer(nickname);
            CreateRoomRestResponse response = await apiClient.Post<CreateRoomRequest, CreateRoomRestResponse>(
                "/rooms/quick-join", new CreateRoomRequest
                {
                    isPublic = true,
                    maxPlayers = createRoomMaxPlayers,
                    timeLimit = createRoomTimeLimitSec,
                });

            if (response == null || string.IsNullOrWhiteSpace(response.code))
                throw new InvalidOperationException("빠른 참가 응답에 방 코드가 없습니다.");

            ConnectAndJoin(response.code, playerIdentity.Nickname);
        }
        catch (Exception exception)
        {
            lobbyController?.ReceiveError(ToUserMessage(exception));
        }
    }

    private async void OnJoinRoomRequested(string nickname, string roomCode)
    {
        try
        {
            await EnsurePlayer(nickname);
            ConnectAndJoin(roomCode, nickname);
        }
        catch (Exception exception)
        {
            lobbyController?.ReceiveError(ToUserMessage(exception));
        }
    }

    // 로그인(또는 게스트 가입)된 플레이어가 있는지 확인한다
    private async System.Threading.Tasks.Task EnsurePlayer(string nickname)
    {
        await silentLoginTask;
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
        lobbyController?.SetLocalPlayer(playerIdentity.PlayerId, playerIdentity.Nickname);
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
        lobbyController?.SetLocalPlayer(playerIdentity.PlayerId, updatedNickname);
        return updatedNickname;
    }

    // 게스트를 허용하지 않으면 구글 로그인 토큰만 로그인으로 친다 (예전에 만든 게스트 토큰이 남아 있어도 로그인 씬으로 보낸다)
    private bool IsLoggedIn()
    {
        return playerIdentity.HasToken && (allowGuestLogin || playerIdentity.IsGoogleLogin);
    }

    public async Task LogoutAsync()
    {
        if (logoutRequested) return;
        logoutRequested = true;
        try
        {
            // 진행 중인 자동 로그인이 로그아웃 후 토큰을 다시 저장하지 않도록 완료를 기다린다.
            await silentLoginTask;
            if (webSocketClient.IsConnected) webSocketClient.Send(MessageType.LEAVE_ROOM);
            await webSocketClient.CloseAndWait();
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

    private void OnUnauthorized()
    {
        playerIdentity.Clear();
        apiClient.Token = null;
        GoToLogin();
    }

    private void GoToLogin()
    {
        UnbindLobbyController();
        pendingRoomCode = null;
        pendingNickname = null;
        if (webSocketClient.IsConnected) webSocketClient.Close();

        if (SceneManager.GetActiveScene().name != loginSceneName)
            SceneManager.LoadScene(loginSceneName);
    }

    private void ConnectAndJoin(string roomCode, string nickname)
    {
        pendingRoomCode = roomCode?.Trim();
        pendingNickname = nickname?.Trim();
        leaveRequested = false;

        if (string.IsNullOrEmpty(pendingRoomCode))
        {
            lobbyController?.ReceiveError("방 코드를 입력해 주세요.");
            return;
        }

        // 서버 확정 주소: ws://{host}/ws/rooms/{roomCode}
        // webSocketUrl에는 방 코드를 제외한 ws://{host}/ws까지만 설정한다.
        webSocketClient.Url = BuildRoomWebSocketUrl(pendingRoomCode);
        if (webSocketClient.IsConnected)
            SendJoinRoom();
        else
            webSocketClient.Connect(playerIdentity.Token);
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
        SendJoinRoom();
    }

    private void SendJoinRoom()
    {
        if (string.IsNullOrEmpty(pendingRoomCode)) return;

        bool sent = webSocketClient.Send(MessageType.JOIN_ROOM, new JoinRoomRequest
        {
            roomCode = pendingRoomCode,
            nickname = pendingNickname,
        });

        if (!sent) lobbyController?.ReceiveError("방 참가 요청을 보내지 못했습니다.");
    }

    private void OnReadyRequested(bool ready)
    {
        if (!CanReadyInCurrentRoom)
        {
            lobbyController?.ReceiveError("재경기는 서버의 방 초기화 기능 연결 후 가능합니다.");
            return;
        }
        if (!webSocketClient.IsConnected ||
            !webSocketClient.Send(MessageType.READY, new ReadyRequest { ready = ready }))
        {
            lobbyController?.ReceiveError("서버에 연결되어 있지 않습니다.");
        }
    }

    private void OnLeaveRoomRequested()
    {
        leaveRequested = true;
        if (webSocketClient.IsConnected) webSocketClient.Send(MessageType.LEAVE_ROOM);
        webSocketClient.Close();
    }

    public void ReturnToWaitingRoom()
    {
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
        _ = RefreshRoomVisibilityAsync(lastRoomState.roomCode);
    }

    public bool CanReadyInCurrentRoom => !gameEnded;

    public async void LeaveGameToMainMenu()
    {
        if (leaveRequested) return;

        leaveRequested = true;
        if (webSocketClient.IsConnected)
            webSocketClient.Send(MessageType.LEAVE_ROOM);
        await webSocketClient.CloseAndWait();

        playerTeams.Clear();
        playerProfiles.Clear();
        pendingRoomCode = null;
        pendingNickname = null;
        leaveRequested = false;
        returnToRoomRequested = false;
        lastRoomState = null;
        SceneManager.LoadScene(lobbySceneName);
    }

    private void OnMessageReceived(WsEnvelope envelope)
    {
        // GAME_START 직후의 개인 선택지·확정 메시지가 씬 로딩 중 사라지지 않도록 보관한다.
        if (waitingForGameSession && envelope.type != MessageType.GAME_START &&
            envelope.type != MessageType.ROOM_STATE && envelope.type != MessageType.PONG)
            pendingGameMessages.Add(envelope);
        try
        {
            switch (envelope.type)
            {
                case MessageType.ROOM_STATE:
                    RoomStateMessage room = MessageSerializer.ReadPayload<RoomStateMessage>(envelope);
                    lastRoomState = room;
                    if (gameEnded && room?.status == "WAITING") gameEnded = false;
                    CachePlayerTeams(room);
                    lobbyController?.ReceiveRoomState(room);
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
                    gameEnded = false;
                    pendingGameMessages.Clear();
                    waitingForGameSession = true;
                    rewardAppliedThisGame = false;
                    GameInstaller.PreparePlayerProfiles(playerProfiles);
                    lobbyController?.ReceiveGameStart(started);
                    break;

                case MessageType.GAME_ENDED:
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
                    lobbyController?.ReceiveError(error?.message ?? "서버 요청이 거절됐습니다.");
                    break;
            }
        }
        catch (Exception exception)
        {
            lobbyController?.ReceiveError($"서버 메시지를 처리하지 못했습니다: {exception.Message}");
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
            lobbyController?.ReceiveRoomVisibility(roomCode, response?.code == roomCode ? response.isPublic : null);
        }
        catch (Exception exception)
        {
            if (version != roomVisibilityRequestVersion || lastRoomState?.roomCode != roomCode) return;
            lobbyController?.ReceiveRoomVisibility(roomCode, null);
            Debug.LogWarning($"[LobbyNetwork] 방 공개 여부 조회 실패: {exception.Message}");
        }
    }

    private void OnSocketError(string message)
    {
        lobbyController?.ReceiveError($"서버 연결 오류: {message}");
    }

    private void OnDisconnected(bool byUser)
    {
        pendingRoomCode = null;
        pendingNickname = null;
        if (logoutRequested) return;

        if (leaveRequested || byUser)
        {
            leaveRequested = false;
            lobbyController?.ReturnToLobby();
            return;
        }

        lobbyController?.ReceiveError("서버 연결이 끊어졌습니다.");
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
