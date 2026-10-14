using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReconnectPanel : UIBase
{
    [SerializeField] private TMP_Text roomText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button closeButton;
    private bool busy;

    public override void Init()
    {
        reconnectButton?.onClick.AddListener(OnReconnectClicked);
        closeButton?.onClick.AddListener(Close);
    }
    private void OnDestroy()
    {
        reconnectButton?.onClick.RemoveListener(OnReconnectClicked);
        closeButton?.onClick.RemoveListener(Close);
    }
    public void ShowRoom(string roomCode)
    {
        if (roomText != null) roomText.text = roomCode;
        ShowStatus("연결이 끊긴 경기가 있습니다.\n서버에서 복귀 가능한지 확인합니다.");
        SetBusy(false);
    }
    protected override void OnClose() => FindFirstObjectByType<LobbyNetworkCoordinator>()?.CancelReconnect();
    private async void OnReconnectClicked()
    {
        if (busy) return;
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator == null) { ShowStatus("서버 연결 정보를 찾을 수 없습니다."); return; }
        SetBusy(true);
        ShowStatus("서버에 재연결하는 중입니다...");
        try { await coordinator.ReconnectSavedGameAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (this != null && IsOpen) ShowStatus(exception.Message); }
        finally { if (this != null) SetBusy(false); }
    }
    private void SetBusy(bool value)
    {
        busy = value;
        if (reconnectButton != null) reconnectButton.interactable = !value;
    }
    private void ShowStatus(string value) { if (statusText != null) statusText.text = value; }
}
