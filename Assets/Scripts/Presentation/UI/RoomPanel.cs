using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// 서버가 보낸 ROOM_STATE를 표시하고 준비·퇴장 입력을 LobbyController에 전달한다.
/// </summary>
public class RoomPanel : UIBase
{
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text roomVisibilityText;
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private GameObject[] playerSlots;
    [SerializeField] private TMP_Text[] playerNameTexts;
    [SerializeField] private TMP_Text[] playerStatusTexts;
    [SerializeField] private Image[] playerSlotBackgrounds;
    [SerializeField] private Image[] playerProfileIcons;
    //[SerializeField] private GameObject[] emptyProfileIconIndicators;
    [SerializeField] private List<Sprite> profileIcons = new();
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button copyRoomCodeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button startGameButton;

    private readonly HashSet<string> reconnectingPlayers = new();
    private RoomStateMessage displayedRoom;
    private string displayedLocalPlayerId;
    private bool localReady;
    private bool isRoomHost;
    private bool interactionEnabled = true;
    private Action requestGameStart;
    private string currentRoomCode;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void Yut_CopyRoomCode(string code, string receiverName);
#endif

    private static readonly Color[] TeamColors =
    {
        new(0.87f, 0.22f, 0.22f, 0.16f),
        new(0.18f, 0.43f, 0.86f, 0.16f),
        new(0.12f, 0.58f, 0.36f, 0.16f),
        new(0.91f, 0.57f, 0.12f, 0.16f),
    };

    public event Action<bool> ReadyRequested;
    public event Action LeaveRequested;
    public event Action SettingsRequested;

    public override void Init()
    {
        readyButton?.onClick.AddListener(OnReadyClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        copyRoomCodeButton?.onClick.AddListener(OnCopyRoomCodeClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        RefreshStartGameButton();
        if (copyRoomCodeButton != null) copyRoomCodeButton.interactable = false;
    }

    private void OnDestroy()
    {
        readyButton?.onClick.RemoveListener(OnReadyClicked);
        leaveButton?.onClick.RemoveListener(OnLeaveClicked);
        copyRoomCodeButton?.onClick.RemoveListener(OnCopyRoomCodeClicked);
        settingsButton?.onClick.RemoveListener(OnSettingsClicked);
        startGameButton?.onClick.RemoveListener(OnStartGameClicked);
    }

    public void ShowRoom(RoomStateMessage room, string localPlayerId)
    {
        if (room == null) return;
        bool roomChanged = currentRoomCode != room.roomCode;
        if (roomChanged)
        {
            reconnectingPlayers.Clear();
            isRoomHost = false;
            requestGameStart = null;
        }
        displayedRoom = room;
        displayedLocalPlayerId = localPlayerId;
        currentRoomCode = room.roomCode;
        if (roomChanged) ShowRoomVisibility(currentRoomCode, null);
        if (copyRoomCodeButton != null)
            copyRoomCodeButton.interactable = !string.IsNullOrWhiteSpace(currentRoomCode);
        if (roomCodeText != null)
            roomCodeText.text = room.roomCode;

        localReady = false;
        // 서버 normalizeTeams는 게임 시작 직전 members 순서로 팀을 배정한다.
        // 대기 중에는 그 배정을 미리 표시하되, 서버의 실제 team 값은 변경하지 않는다.
        bool previewGameTeams = room.status == "WAITING";
        RoomPlayer[] members = previewGameTeams
            ? room.members.ToArray()
            : room.members.OrderBy(player => player.team).ToArray();
        for (int i = 0; i < playerSlots.Length; i++)
        {
            bool occupied = i < members.Length;
            RoomPlayer player = occupied ? members[i] : null;
            int displayTeam = previewGameTeams ? i : (player?.team ?? i);
            playerSlots[i]?.SetActive(true);

            if (playerNameTexts != null && i < playerNameTexts.Length && playerNameTexts[i] != null)
                playerNameTexts[i].text = occupied
                    ? $"{player.nickname}{(player.playerId == localPlayerId ? "  (나)" : string.Empty)}"
                    : "참가 대기";

            if (playerProfileIcons != null && i < playerProfileIcons.Length && playerProfileIcons[i] != null)
            {
                Image icon = playerProfileIcons[i];
                icon.sprite = occupied ? PlayerProfileIconResolver.Resolve(profileIcons, player.iconId, displayTeam) : null;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            //if (emptyProfileIconIndicators != null && i < emptyProfileIconIndicators.Length)
            //    emptyProfileIconIndicators[i]?.SetActive(!occupied);

            if (playerStatusTexts != null && i < playerStatusTexts.Length && playerStatusTexts[i] != null)
            {
                bool reconnecting = occupied && reconnectingPlayers.Contains(player.playerId);
                playerStatusTexts[i].text = reconnecting ? "재연결 대기 중..."
                    : occupied ? (player.ready ? "준비 완료" : "대기 중") : "빈 자리";
                playerStatusTexts[i].color = reconnecting ? new Color32(164, 108, 43, 255)
                    : occupied && player.ready
                    ? Color.white
                    : occupied ? new Color32(122, 92, 62, 255) : new Color32(168, 142, 110, 255);
            }

            playerSlots[i]?.GetComponent<YutNori.UI.Common.RoomSlotPresentation>()?.Show(
                occupied, occupied && player.ready, occupied && reconnectingPlayers.Contains(player.playerId));

            if (playerSlotBackgrounds != null && i < playerSlotBackgrounds.Length && playerSlotBackgrounds[i] != null)
                playerSlotBackgrounds[i].color = occupied
                    ? TeamColors[Mathf.Clamp(displayTeam, 0, TeamColors.Length - 1)]
                    : new Color32(235, 228, 211, 120);

            if (occupied && player.playerId == localPlayerId) localReady = player.ready;
        }

        if (playersText != null) playersText.gameObject.SetActive(false);
        RefreshReadyLabel();
        RefreshStartGameButton();
    }

    public void ShowReconnectingPlayers(IEnumerable<string> playerIds)
    {
        reconnectingPlayers.Clear();
        if (playerIds != null)
            foreach (string playerId in playerIds)
                if (!string.IsNullOrEmpty(playerId)) reconnectingPlayers.Add(playerId);
        if (displayedRoom != null) ShowRoom(displayedRoom, displayedLocalPlayerId);
    }

    public void ShowRoomVisibility(string roomCode, bool? isPublic)
    {
        if (roomCode != currentRoomCode || roomVisibilityText == null) return;
        roomVisibilityText.text = !isPublic.HasValue
            ? "공개 여부 확인 중..."
            : isPublic.Value ? "공개방" : "비공개방";
        roomVisibilityText.color = new Color32(122, 92, 62, 255);
    }

    public void ShowRoomVisibilityUnavailable(string roomCode)
    {
        if (roomCode != currentRoomCode || roomVisibilityText == null) return;
        roomVisibilityText.text = "공개 여부를 확인하지 못했습니다.";
    }

    public void SetInteraction(bool interactable)
    {
        interactionEnabled = interactable;
        if (readyButton != null) readyButton.interactable = interactable;
        if (leaveButton != null) leaveButton.interactable = interactable;
        RefreshStartGameButton();
    }

    // ROOM_STATE 표시 후 서버의 hostPlayerId 판정과 시작 요청 콜백을 연결한다.
    // 방장 정보가 없거나 요청이 연결되지 않으면 시작 버튼을 숨긴다.
    public void ConfigureGameStart(bool isHost, Action requestStart)
    {
        isRoomHost = isHost;
        requestGameStart = requestStart;
        RefreshStartGameButton();
    }

    private void RefreshStartGameButton()
    {
        if (startGameButton == null) return;
        bool canStart = displayedRoom?.status == "WAITING" &&
            displayedRoom.members != null && displayedRoom.members.Count >= 2 &&
            displayedRoom.members.Count <= 4 && displayedRoom.members.All(player =>
                player.ready && !reconnectingPlayers.Contains(player.playerId));
        bool visible = isRoomHost && requestGameStart != null && canStart;
        startGameButton.gameObject.SetActive(visible);
        startGameButton.interactable = visible && interactionEnabled;
    }

    private void OnStartGameClicked()
    {
        if (startGameButton == null || !startGameButton.gameObject.activeSelf || !startGameButton.interactable) return;
        SetInteraction(false);
        ShowStatus("게임 시작을 요청하는 중입니다.");
        requestGameStart?.Invoke();
    }

    public void SetReadyInteractable(bool interactable)
    {
        if (readyButton != null) readyButton.interactable = interactable;
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private void OnReadyClicked()
    {
        // 화면은 서버 ROOM_STATE가 돌아온 뒤 갱신한다. 클릭 즉시 localReady를 바꾸지 않는다.
        ReadyRequested?.Invoke(!localReady);
    }

    private void OnCopyRoomCodeClicked()
    {
        if (string.IsNullOrWhiteSpace(currentRoomCode)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        Yut_CopyRoomCode(currentRoomCode, gameObject.name);
#else
        GUIUtility.systemCopyBuffer = currentRoomCode;
        OnRoomCodeCopySucceeded();
#endif
    }

    public void OnRoomCodeCopySucceeded() => ShowStatus("방 코드를 복사했습니다.");
    public void OnRoomCodeCopyFailed() => ShowStatus("복사하지 못했습니다. 표시된 방 코드를 직접 입력해 주세요.");

    private void OnLeaveClicked() => LeaveRequested?.Invoke();
    private void OnSettingsClicked() => SettingsRequested?.Invoke();

    private void RefreshReadyLabel()
    {
        if (readyButtonText != null) readyButtonText.text = localReady ? "준비 취소" : "준비";
        readyButton?.GetComponent<YutNori.UI.Common.RoomReadyButtonPresentation>()?.SetReady(localReady);
    }
}
