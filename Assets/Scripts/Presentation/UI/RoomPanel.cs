using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 서버가 보낸 ROOM_STATE를 표시하고 준비·퇴장 입력을 LobbyController에 전달한다.
/// </summary>
public class RoomPanel : UIBase
{
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button leaveButton;

    private bool localReady;

    public event Action<bool> ReadyRequested;
    public event Action LeaveRequested;

    public override void Init()
    {
        readyButton?.onClick.AddListener(OnReadyClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
    }

    private void OnDestroy()
    {
        readyButton?.onClick.RemoveListener(OnReadyClicked);
        leaveButton?.onClick.RemoveListener(OnLeaveClicked);
    }

    public void ShowRoom(RoomStateMessage room, string localPlayerId)
    {
        if (room == null) return;
        if (roomCodeText != null) roomCodeText.text = room.roomCode ?? string.Empty;

        var builder = new StringBuilder();
        localReady = false;
        foreach (RoomPlayer player in room.players)
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"Team {player.team + 1} · {player.nickname}");
            builder.Append(player.ready ? " · 준비" : " · 대기");
            if (player.playerId == localPlayerId) localReady = player.ready;
        }

        if (playersText != null) playersText.text = builder.ToString();
        RefreshReadyLabel();
    }

    public void SetInteraction(bool interactable)
    {
        if (readyButton != null) readyButton.interactable = interactable;
        if (leaveButton != null) leaveButton.interactable = interactable;
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

    private void OnLeaveClicked() => LeaveRequested?.Invoke();

    private void RefreshReadyLabel()
    {
        if (readyButtonText != null) readyButtonText.text = localReady ? "준비 취소" : "준비";
    }
}
