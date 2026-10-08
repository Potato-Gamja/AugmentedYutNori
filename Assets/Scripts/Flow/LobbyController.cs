using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 UI와 네트워크 계층 사이의 연결 지점이다.
/// UI는 이 Controller에 입력을 전달하고, 네트워크 계층은 Receive 메서드로 서버 결과를 전달한다.
/// </summary>
public class LobbyController : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private string gameSceneName = "LSU_Game_UI";

    private LobbyPanel lobbyPanel;
    private RoomPanel roomPanel;
    private string localPlayerId;

    // 네트워크 계약:
    // CreateRoomRequested -> ApiClient에서 POST /players 후 POST /rooms 호출.
    // JoinRoomRequested   -> POST /players 후 WebSocketClient.Connect(roomCode, guestToken), JOIN_ROOM 전송.
    // ReadyRequested      -> WebSocket으로 READY 전송.
    // LeaveRoomRequested  -> WebSocket으로 LEAVE_ROOM 전송 후 연결 해제.
    // ApiClient/WebSocketClient가 머지되면 해당 구현을 이 이벤트에 연결하거나 아래 요청 메서드 내부로 옮긴다.
    public event Action<string> CreateRoomRequested;
    public event Action<string, string> JoinRoomRequested;
    public event Action<bool> ReadyRequested;
    public event Action LeaveRoomRequested;

    private void Start()
    {
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("[Lobby] UIManager가 없습니다.", this);
            enabled = false;
            return;
        }

        lobbyPanel = uiManager.Open<LobbyPanel>();
        roomPanel = uiManager.Get<RoomPanel>();
        if (lobbyPanel == null || roomPanel == null)
        {
            Debug.LogError("[Lobby] LobbyPanel과 RoomPanel을 UIManager 프리팹 목록에 등록해 주세요.", this);
            enabled = false;
            return;
        }

        lobbyPanel.CreateRoomRequested += OnCreateRoomRequested;
        lobbyPanel.JoinRoomRequested += OnJoinRoomRequested;
        lobbyPanel.SettingsRequested += OnSettingsRequested;
        roomPanel.ReadyRequested += OnReadyRequested;
        roomPanel.LeaveRequested += OnLeaveRequested;
    }

    private void OnDestroy()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.CreateRoomRequested -= OnCreateRoomRequested;
            lobbyPanel.JoinRoomRequested -= OnJoinRoomRequested;
            lobbyPanel.SettingsRequested -= OnSettingsRequested;
        }
        if (roomPanel != null)
        {
            roomPanel.ReadyRequested -= OnReadyRequested;
            roomPanel.LeaveRequested -= OnLeaveRequested;
        }
    }

    private void OnSettingsRequested()
    {
        // UI 조립 담당 확인: SettingsPanel 프리팹을 로비 UIManager에 등록하고 settingsButton을 연결한다.
        uiManager?.Open<SettingsPanel>();
    }

    private void OnCreateRoomRequested(string nickname)
    {
        lobbyPanel.SetBusy(true);
        lobbyPanel.ShowStatus("방을 생성하는 중입니다.");
        CreateRoomRequested?.Invoke(nickname);
    }

    private void OnJoinRoomRequested(string nickname, string roomCode)
    {
        lobbyPanel.SetBusy(true);
        lobbyPanel.ShowStatus("방에 참가하는 중입니다.");
        JoinRoomRequested?.Invoke(nickname, roomCode);
    }

    private void OnReadyRequested(bool ready)
    {
        roomPanel.SetInteraction(false);
        roomPanel.ShowStatus("서버 응답을 기다리는 중입니다.");
        ReadyRequested?.Invoke(ready);
    }

    private void OnLeaveRequested()
    {
        roomPanel.SetInteraction(false);
        LeaveRoomRequested?.Invoke();
    }

    // PlayerIdentity/ApiClient 계약: POST /players 응답의 playerId를 전달한다.
    public void SetLocalPlayer(string playerId)
    {
        localPlayerId = playerId;
    }

    // WebSocketClient 계약: ROOM_STATE payload를 역직렬화한 뒤 호출한다.
    public void ReceiveRoomState(RoomStateMessage room)
    {
        uiManager.Close<LobbyPanel>();
        uiManager.Open<RoomPanel>();
        roomPanel.ShowRoom(room, localPlayerId);
        roomPanel.SetInteraction(true);
        roomPanel.ShowStatus("다른 플레이어를 기다리는 중입니다.");
    }

    // WebSocketClient 계약: GAME_START payload를 역직렬화한 뒤 호출한다.
    // GameInstaller가 메시지를 보관하고 다음 씬의 GameFlowController 시작 전에 GameSetup을 적용한다.
    public void ReceiveGameStart(GameStartMessage message)
    {
        if (message == null)
        {
            ReceiveError("GAME_START 내용이 비어 있습니다.");
            return;
        }

        GameInstaller.PrepareOnlineGame(message);
        SceneManager.LoadScene(gameSceneName);
    }

    // REST 실패, WebSocket ERROR, 연결 실패를 같은 경로로 표시한다.
    public void ReceiveError(string message)
    {
        lobbyPanel?.SetBusy(false);
        lobbyPanel?.ShowStatus(message);
        roomPanel?.SetInteraction(true);
        roomPanel?.ShowStatus(message);
    }

    // LEAVE_ROOM 처리 또는 연결 해제가 완료되면 네트워크 계층에서 호출한다.
    public void ReturnToLobby()
    {
        uiManager.Close<RoomPanel>();
        uiManager.Open<LobbyPanel>();
        lobbyPanel.SetBusy(false);
        lobbyPanel.ShowStatus(string.Empty);
    }
}
