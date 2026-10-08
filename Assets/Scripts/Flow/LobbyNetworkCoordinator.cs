using System;
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

    [Header("로그인")]
    [SerializeField] private string loginSceneName = "PJWScene_Login";
    // 로그인하지 않은 상태에서 방을 만들거나 참가하면 게스트로 자동 가입한다 (서버 /auth/firebase 준비 전·에디터 테스트용).
    // 끄면 로그인 화면으로 돌려보낸다
    [SerializeField] private bool allowGuestLogin = true;

    private static LobbyNetworkCoordinator instance;

    private ApiClient apiClient;
    private AuthApi authApi;
    private PlayerIdentity playerIdentity;
    private string pendingRoomCode;
    private string pendingNickname;
    private bool leaveRequested;

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
        playerIdentity = new PlayerIdentity(playerPrefsKeyPrefix);
        apiClient.Token = playerIdentity.Token;
        apiClient.Unauthorized += OnUnauthorized;
        webSocketClient.Url = webSocketUrl;

        // 게임 씬의 GameInstaller가 같은 WebSocket 연결로 RemoteGameSession을 만들 수 있게 등록한다.
        GameInstaller.RegisterRemoteSessionFactory((rules, events, state, turn, setup) =>
            new RemoteGameSession(webSocketClient, rules, events, state, turn, setup));

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
        // 로그인 씬에서 새로 저장한 토큰을 반영한다
        playerIdentity.Reload();
        apiClient.Token = playerIdentity.Token;
        BindLobbyController();
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
        lobbyController.ReadyRequested += OnReadyRequested;
        lobbyController.LeaveRoomRequested += OnLeaveRoomRequested;

        if (playerIdentity.HasToken)
            lobbyController.SetLocalPlayer(playerIdentity.PlayerId);
        else if (!allowGuestLogin)
            GoToLogin();
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null) return;
        lobbyController.CreateRoomRequested -= OnCreateRoomRequested;
        lobbyController.JoinRoomRequested -= OnJoinRoomRequested;
        lobbyController.ReadyRequested -= OnReadyRequested;
        lobbyController.LeaveRoomRequested -= OnLeaveRoomRequested;
        lobbyController = null;
    }

    private async void OnCreateRoomRequested(string nickname)
    {
        try
        {
            await EnsurePlayer(nickname);

            // 서버 계약 확인 필요:
            // 현재는 설계표의 REST 방식인 POST /rooms를 사용한다.
            // 서버가 CREATE_ROOM WebSocket 방식으로 확정되면 이 호출만 메시지 전송으로 교체한다.
            CreateRoomRestResponse response = await apiClient.Post<CreateRoomRequest, CreateRoomRestResponse>(
                "/rooms", new CreateRoomRequest { nickname = nickname });

            if (response == null || string.IsNullOrWhiteSpace(response.roomCode))
                throw new InvalidOperationException("방 생성 응답에 roomCode가 없습니다.");

            ConnectAndJoin(response.roomCode, nickname);
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
        if (!playerIdentity.HasToken)
        {
            if (!allowGuestLogin)
            {
                GoToLogin();
                throw new InvalidOperationException("로그인이 필요합니다.");
            }

            await playerIdentity.EnsureGuestAsync(authApi, nickname);
        }

        apiClient.Token = playerIdentity.Token;
        lobbyController?.SetLocalPlayer(playerIdentity.PlayerId);
    }

    // 서버가 토큰을 거절(401)하면 저장된 토큰을 지우고 로그인 화면으로 보낸다
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
        if (webSocketClient.IsConnected) webSocketClient.Close();
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

    private void OnMessageReceived(WsEnvelope envelope)
    {
        try
        {
            switch (envelope.type)
            {
                case MessageType.ROOM_STATE:
                    lobbyController?.ReceiveRoomState(
                        MessageSerializer.ReadPayload<RoomStateMessage>(envelope));
                    break;

                case MessageType.GAME_START:
                    lobbyController?.ReceiveGameStart(
                        MessageSerializer.ReadPayload<GameStartMessage>(envelope));
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

    private void OnSocketError(string message)
    {
        lobbyController?.ReceiveError($"서버 연결 오류: {message}");
    }

    private void OnDisconnected(bool byUser)
    {
        pendingRoomCode = null;
        pendingNickname = null;

        if (leaveRequested || byUser)
        {
            leaveRequested = false;
            lobbyController?.ReturnToLobby();
            return;
        }

        lobbyController?.ReceiveError("서버 연결이 끊어졌습니다.");
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
    public string roomCode;
}
