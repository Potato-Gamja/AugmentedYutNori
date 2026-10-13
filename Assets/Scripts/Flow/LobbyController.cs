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
    [SerializeField] private string gameSceneName = "Game";

    private LobbyPanel lobbyPanel;
    private MainMenuPanel mainMenuPanel;
    private RoomPanel roomPanel;
    private ProfilePanel profilePanel;
    private string localPlayerId;

    public bool IsPublicRoom => lobbyPanel == null || lobbyPanel.IsPublicRoom;

    // 네트워크 계약:
    // CreateRoomRequested -> ApiClient에서 POST /players 후 POST /rooms 호출.
    // JoinRoomRequested   -> POST /players 후 WebSocketClient.Connect(roomCode, guestToken), JOIN_ROOM 전송.
    // QuickJoinRequested  -> 서버 빠른 참가 API가 참가할 roomCode를 반환하면 기존 JOIN_ROOM 흐름으로 연결.
    // ReadyRequested      -> WebSocket으로 READY 전송.
    // LeaveRoomRequested  -> WebSocket으로 LEAVE_ROOM 전송 후 연결 해제.
    // ApiClient/WebSocketClient가 머지되면 해당 구현을 이 이벤트에 연결하거나 아래 요청 메서드 내부로 옮긴다.
    public event Action<string> CreateRoomRequested;
    public event Action<string, string> JoinRoomRequested;
    public event Action<string> QuickJoinRequested;
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

        mainMenuPanel = uiManager.Open<MainMenuPanel>();
        lobbyPanel = uiManager.Get<LobbyPanel>();
        roomPanel = uiManager.Get<RoomPanel>();
        if (mainMenuPanel == null || lobbyPanel == null || roomPanel == null)
        {
            Debug.LogError("[Lobby] MainMenuPanel, LobbyPanel, RoomPanel을 UIManager 프리팹 목록에 등록해 주세요.", this);
            enabled = false;
            return;
        }

        lobbyPanel.CreateRoomRequested += OnCreateRoomRequested;
        lobbyPanel.JoinRoomRequested += OnJoinRoomRequested;
        lobbyPanel.QuickJoinRequested += OnQuickJoinRequested;
        lobbyPanel.MainMenuRequested += OnMainMenuRequested;
        lobbyPanel.SettingsRequested += OnSettingsRequested;
        lobbyPanel.CollectionRequested += OnCollectionRequested;
        roomPanel.ReadyRequested += OnReadyRequested;
        roomPanel.LeaveRequested += OnLeaveRequested;
        mainMenuPanel.PlayRequested += OnPlayRequested;
        mainMenuPanel.GachaRequested += OnGachaRequested;
        mainMenuPanel.CollectionRequested += OnCollectionRequested;
        mainMenuPanel.ProfileRequested += OnProfileRequested;
        mainMenuPanel.AchievementRequested += OnAchievementRequested;
        mainMenuPanel.SettingsRequested += OnSettingsRequested;
        FindFirstObjectByType<LobbyNetworkCoordinator>()?.RestoreWaitingRoom(this);
    }

    private void OnDestroy()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.CreateRoomRequested -= OnCreateRoomRequested;
            lobbyPanel.JoinRoomRequested -= OnJoinRoomRequested;
            lobbyPanel.QuickJoinRequested -= OnQuickJoinRequested;
            lobbyPanel.MainMenuRequested -= OnMainMenuRequested;
            lobbyPanel.SettingsRequested -= OnSettingsRequested;
            lobbyPanel.CollectionRequested -= OnCollectionRequested;
        }
        if (roomPanel != null)
        {
            roomPanel.ReadyRequested -= OnReadyRequested;
            roomPanel.LeaveRequested -= OnLeaveRequested;
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.PlayRequested -= OnPlayRequested;
            mainMenuPanel.GachaRequested -= OnGachaRequested;
            mainMenuPanel.CollectionRequested -= OnCollectionRequested;
            mainMenuPanel.ProfileRequested -= OnProfileRequested;
            mainMenuPanel.AchievementRequested -= OnAchievementRequested;
            mainMenuPanel.SettingsRequested -= OnSettingsRequested;
        }
        if (profilePanel != null)
            profilePanel.LogoutRequested -= OnLogoutRequested;
    }

    private void OnSettingsRequested()
    {
        SettingsPanel panel = uiManager?.Open<SettingsPanel>();
        panel?.SetExitGameVisible(false);
    }

    private void OnCollectionRequested()
    {
        uiManager?.Open<CollectionPanel>();
    }

    private void OnPlayRequested()
    {
        uiManager.Close<MainMenuPanel>();
        uiManager.Open<LobbyPanel>();
        lobbyPanel.ShowStatus(string.Empty);
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        lobbyPanel.SetNickname(coordinator?.LocalNickname);
    }

    private void OnMainMenuRequested()
    {
        uiManager.Close<LobbyPanel>();
        uiManager.Open<MainMenuPanel>();
    }

    private void OnGachaRequested() => uiManager?.Open<GachaPanel>();
    private void OnProfileRequested()
    {
        profilePanel = uiManager?.Open<ProfilePanel>();
        if (profilePanel == null) return;

        profilePanel.LogoutRequested -= OnLogoutRequested;
        profilePanel.LogoutRequested += OnLogoutRequested;
        profilePanel.SetLogoutBusy(false);

        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        profilePanel.ShowProfile(coordinator?.LocalPlayerId, coordinator?.LocalNickname);
    }
    private async void OnLogoutRequested()
    {
        if (profilePanel == null) return;

        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator == null)
        {
            profilePanel.ShowStatus("로그인 정보를 찾을 수 없습니다.");
            return;
        }

        profilePanel.SetLogoutBusy(true);
        profilePanel.ShowStatus("로그아웃 중입니다.");
        try
        {
            await coordinator.LogoutAsync();
        }
        catch (Exception exception)
        {
            if (profilePanel != null) profilePanel.ShowStatus($"로그아웃하지 못했습니다: {exception.Message}");
        }
        finally
        {
            if (profilePanel != null) profilePanel.SetLogoutBusy(false);
        }
    }
    private void OnAchievementRequested() => mainMenuPanel.ShowStatus("업적 기능은 준비 중입니다.");

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

    private void OnQuickJoinRequested(string nickname)
    {
        // 서버 담당 연결 지점: 참가 가능한 방 조회/생성 후 roomCode를 받아 기존 참가 흐름으로 이어 준다.
        if (QuickJoinRequested == null)
        {
            lobbyPanel.ShowStatus("빠른 참가 서버 기능을 준비 중입니다.");
            return;
        }

        lobbyPanel.SetBusy(true);
        lobbyPanel.ShowStatus("참가 가능한 방을 찾는 중입니다.");
        QuickJoinRequested.Invoke(nickname);
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
    public void SetLocalPlayer(string playerId, string nickname = null)
    {
        localPlayerId = playerId;
        if (!string.IsNullOrWhiteSpace(nickname)) lobbyPanel?.SetNickname(nickname);
    }

    // WebSocketClient 계약: ROOM_STATE payload를 역직렬화한 뒤 호출한다.
    public void ReceiveRoomState(RoomStateMessage room)
    {
        uiManager.Close<MainMenuPanel>();
        uiManager.Close<LobbyPanel>();
        uiManager.Open<RoomPanel>();
        roomPanel.ShowRoom(room, localPlayerId);
        roomPanel.SetInteraction(true);
        roomPanel.ShowStatus("다른 플레이어를 기다리는 중입니다.");
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator != null && !coordinator.CanReadyInCurrentRoom)
        {
            roomPanel.SetReadyInteractable(false);
            roomPanel.ShowStatus("경기가 종료되었습니다. 재경기는 서버의 방 초기화 기능 연결 후 가능합니다.");
        }
    }

    public void ReceiveRoomVisibility(string roomCode, bool? isPublic)
    {
        if (isPublic.HasValue) roomPanel?.ShowRoomVisibility(roomCode, isPublic);
        else roomPanel?.ShowRoomVisibilityUnavailable(roomCode);
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
