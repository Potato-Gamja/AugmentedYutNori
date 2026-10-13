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
    [SerializeField] private List<Sprite> profileIcons = new();
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button copyRoomCodeButton;

    private bool localReady;
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

    public override void Init()
    {
        readyButton?.onClick.AddListener(OnReadyClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        copyRoomCodeButton?.onClick.AddListener(OnCopyRoomCodeClicked);
        if (copyRoomCodeButton != null) copyRoomCodeButton.interactable = false;
    }

    private void OnDestroy()
    {
        readyButton?.onClick.RemoveListener(OnReadyClicked);
        leaveButton?.onClick.RemoveListener(OnLeaveClicked);
        copyRoomCodeButton?.onClick.RemoveListener(OnCopyRoomCodeClicked);
    }

    public void ShowRoom(RoomStateMessage room, string localPlayerId)
    {
        if (room == null) return;
        bool roomChanged = currentRoomCode != room.roomCode;
        currentRoomCode = room.roomCode;
        if (roomChanged) ShowRoomVisibility(currentRoomCode, null);
        if (copyRoomCodeButton != null)
            copyRoomCodeButton.interactable = !string.IsNullOrWhiteSpace(currentRoomCode);
        if (roomCodeText != null)
            roomCodeText.text = $"방 코드  {room.roomCode}";

        localReady = false;
        RoomPlayer[] members = room.members.OrderBy(player => player.team).ToArray();
        for (int i = 0; i < playerSlots.Length; i++)
        {
            bool occupied = i < members.Length;
            RoomPlayer player = occupied ? members[i] : null;
            playerSlots[i]?.SetActive(true);

            if (playerNameTexts != null && i < playerNameTexts.Length && playerNameTexts[i] != null)
                playerNameTexts[i].text = occupied
                    ? $"{player.nickname}{(player.playerId == localPlayerId ? "  (나)" : string.Empty)}"
                    : "참가 대기";

            if (playerProfileIcons != null && i < playerProfileIcons.Length && playerProfileIcons[i] != null)
            {
                Image icon = playerProfileIcons[i];
                icon.sprite = occupied ? PlayerProfileIconResolver.Resolve(profileIcons, player.iconId, player.team) : null;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            if (playerStatusTexts != null && i < playerStatusTexts.Length && playerStatusTexts[i] != null)
            {
                playerStatusTexts[i].text = occupied ? (player.ready ? "준비 완료" : "대기 중") : "빈 자리";
                playerStatusTexts[i].color = occupied && player.ready
                    ? new Color32(31, 145, 87, 255)
                    : new Color32(135, 120, 100, 255);
            }

            if (playerSlotBackgrounds != null && i < playerSlotBackgrounds.Length && playerSlotBackgrounds[i] != null)
                playerSlotBackgrounds[i].color = occupied
                    ? TeamColors[Mathf.Clamp(player.team, 0, TeamColors.Length - 1)]
                    : new Color32(235, 228, 211, 120);

            if (occupied && player.playerId == localPlayerId) localReady = player.ready;
        }

        if (playersText != null) playersText.gameObject.SetActive(false);
        RefreshReadyLabel();
    }

    public void ShowRoomVisibility(string roomCode, bool? isPublic)
    {
        if (roomCode != currentRoomCode || roomVisibilityText == null) return;
        roomVisibilityText.text = !isPublic.HasValue
            ? "공개 여부 확인 중..."
            : isPublic.Value ? "공개방 · 빠른 참가 가능" : "비공개방 · 코드로만 참가 가능";
        roomVisibilityText.color = isPublic == true
            ? new Color32(31, 145, 87, 255)
            : new Color32(135, 100, 60, 255);
    }

    public void ShowRoomVisibilityUnavailable(string roomCode)
    {
        if (roomCode != currentRoomCode || roomVisibilityText == null) return;
        roomVisibilityText.text = "공개 여부를 확인하지 못했습니다.";
    }

    public void SetInteraction(bool interactable)
    {
        if (readyButton != null) readyButton.interactable = interactable;
        if (leaveButton != null) leaveButton.interactable = interactable;
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

    private void RefreshReadyLabel()
    {
        if (readyButtonText != null) readyButtonText.text = localReady ? "준비 취소" : "준비";
    }
}
