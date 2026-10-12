using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 입력과 표시만 담당한다. REST/WebSocket 요청은 LobbyController를 거쳐 처리한다.
/// </summary>
public class LobbyPanel : UIBase
{
    [Header("입력")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("버튼")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button collectionButton;

    [Header("상태")]
    [SerializeField] private TMP_Text statusText;

    public event Action<string> CreateRoomRequested;
    public event Action<string, string> JoinRoomRequested;
    public event Action<string> QuickJoinRequested;
    public event Action MainMenuRequested;
    public event Action SettingsRequested;
    public event Action CollectionRequested;

    public override void Init()
    {
        createRoomButton?.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton?.onClick.AddListener(OnJoinRoomClicked);
        quickJoinButton?.onClick.AddListener(OnQuickJoinClicked);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        collectionButton?.onClick.AddListener(OnCollectionClicked);
    }

    private void OnDestroy()
    {
        createRoomButton?.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton?.onClick.RemoveListener(OnJoinRoomClicked);
        quickJoinButton?.onClick.RemoveListener(OnQuickJoinClicked);
        mainMenuButton?.onClick.RemoveListener(OnMainMenuClicked);
        settingsButton?.onClick.RemoveListener(OnSettingsClicked);
        collectionButton?.onClick.RemoveListener(OnCollectionClicked);
    }

    private void OnSettingsClicked() => SettingsRequested?.Invoke();
    private void OnCollectionClicked() => CollectionRequested?.Invoke();
    private void OnMainMenuClicked() => MainMenuRequested?.Invoke();

    public void SetBusy(bool busy)
    {
        if (createRoomButton != null) createRoomButton.interactable = !busy;
        if (joinRoomButton != null) joinRoomButton.interactable = !busy;
        if (quickJoinButton != null) quickJoinButton.interactable = !busy;
        if (nicknameInput != null) nicknameInput.interactable = !busy;
        if (roomCodeInput != null) roomCodeInput.interactable = !busy;
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    public void SetNickname(string nickname)
    {
        if (nicknameInput != null)
            nicknameInput.SetTextWithoutNotify(nickname ?? string.Empty);
    }

    private void OnCreateRoomClicked()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(nickname))
        {
            ShowStatus("닉네임을 입력해 주세요.");
            return;
        }

        CreateRoomRequested?.Invoke(nickname);
    }

    private void OnJoinRoomClicked()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        string roomCode = roomCodeInput != null ? roomCodeInput.text.Trim().ToUpperInvariant() : string.Empty;
        if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(roomCode))
        {
            ShowStatus("닉네임과 방 코드를 입력해 주세요.");
            return;
        }

        JoinRoomRequested?.Invoke(nickname, roomCode);
    }

    private void OnQuickJoinClicked()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(nickname))
        {
            ShowStatus("닉네임을 입력해 주세요.");
            return;
        }

        QuickJoinRequested?.Invoke(nickname);
    }

}
