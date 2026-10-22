using System;
using System.Collections;
using YutNori.UI.Common;
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

    [Header("방 공개 여부")]
    [SerializeField] private Button publicRoomButton;
    [SerializeField] private Button privateRoomButton;
    [SerializeField] private TMP_Text roomVisibilityText;

    public bool IsPublicRoom { get; private set; } = true;

    [Header("버튼")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button collectionButton;

    [Header("상태")]
    [SerializeField] private TMP_Text statusText;

    [Header("입장 창")]
    [SerializeField] private GameObject createRoomWindow;
    [SerializeField] private GameObject joinRoomWindow;
    [SerializeField] private Button openCreateRoomButton;
    [SerializeField] private Button openJoinRoomButton;
    [SerializeField] private Button closeCreateRoomButton;
    [SerializeField] private Button closeJoinRoomButton;
    [SerializeField] private Button createRoomDimButton;
    [SerializeField] private Button joinRoomDimButton;
    [SerializeField] private TMP_Text createRoomStatusText;
    [SerializeField] private TMP_Text joinRoomStatusText;
    private bool busy;

    public event Action<string> CreateRoomRequested;
    public event Action<string, string> JoinRoomRequested;
    public event Action<string> QuickJoinRequested;
    public event Action MainMenuRequested;
    public event Action SettingsRequested;
    public event Action CollectionRequested;

    public override void Init()
    {
        openCreateRoomButton?.onClick.AddListener(OpenCreateRoomWindow);
        openJoinRoomButton?.onClick.AddListener(OpenJoinRoomWindow);
        closeCreateRoomButton?.onClick.AddListener(CloseCreateRoomWindow);
        closeJoinRoomButton?.onClick.AddListener(CloseJoinRoomWindow);
        createRoomDimButton?.onClick.AddListener(CloseCreateRoomWindow);
        joinRoomDimButton?.onClick.AddListener(CloseJoinRoomWindow);
        roomCodeInput?.onValueChanged.AddListener(OnRoomCodeChanged);
        roomCodeInput?.onSubmit.AddListener(OnRoomCodeSubmitted);
        publicRoomButton?.onClick.AddListener(OnPublicRoomClicked);
        privateRoomButton?.onClick.AddListener(OnPrivateRoomClicked);
        RefreshRoomVisibility();
        createRoomButton?.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton?.onClick.AddListener(OnJoinRoomClicked);
        quickJoinButton?.onClick.AddListener(OnQuickJoinClicked);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        collectionButton?.onClick.AddListener(OnCollectionClicked);
        SetBusy(false);
    }

    private void OnDestroy()
    {
        openCreateRoomButton?.onClick.RemoveListener(OpenCreateRoomWindow);
        openJoinRoomButton?.onClick.RemoveListener(OpenJoinRoomWindow);
        closeCreateRoomButton?.onClick.RemoveListener(CloseCreateRoomWindow);
        closeJoinRoomButton?.onClick.RemoveListener(CloseJoinRoomWindow);
        createRoomDimButton?.onClick.RemoveListener(CloseCreateRoomWindow);
        joinRoomDimButton?.onClick.RemoveListener(CloseJoinRoomWindow);
        roomCodeInput?.onValueChanged.RemoveListener(OnRoomCodeChanged);
        roomCodeInput?.onSubmit.RemoveListener(OnRoomCodeSubmitted);
        publicRoomButton?.onClick.RemoveListener(OnPublicRoomClicked);
        privateRoomButton?.onClick.RemoveListener(OnPrivateRoomClicked);
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
        this.busy = busy;
        if (openCreateRoomButton != null) openCreateRoomButton.interactable = !busy;
        if (openJoinRoomButton != null) openJoinRoomButton.interactable = !busy;
        if (closeCreateRoomButton != null) closeCreateRoomButton.interactable = !busy;
        if (closeJoinRoomButton != null) closeJoinRoomButton.interactable = !busy;
        if (createRoomDimButton != null) createRoomDimButton.interactable = !busy;
        if (joinRoomDimButton != null) joinRoomDimButton.interactable = !busy;
        if (publicRoomButton != null) publicRoomButton.interactable = !busy;
        if (privateRoomButton != null) privateRoomButton.interactable = !busy;
        if (createRoomButton != null) createRoomButton.interactable = !busy;
        RefreshJoinButton();
        if (quickJoinButton != null) quickJoinButton.interactable = !busy;
        if (nicknameInput != null) nicknameInput.interactable = !busy;
        if (roomCodeInput != null) roomCodeInput.interactable = !busy;
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
        if (createRoomWindow != null && createRoomWindow.activeSelf && createRoomStatusText != null)
            SetWindowStatus(createRoomStatusText, message);
        if (joinRoomWindow != null && joinRoomWindow.activeSelf && joinRoomStatusText != null)
            SetWindowStatus(joinRoomStatusText, message);
    }

    public void SetNickname(string nickname)
    {
        if (nicknameInput != null)
            nicknameInput.SetTextWithoutNotify(nickname ?? string.Empty);
    }

    private void OnPublicRoomClicked()
    {
        IsPublicRoom = true;
        RefreshRoomVisibility();
    }

    private void OnPrivateRoomClicked()
    {
        IsPublicRoom = false;
        RefreshRoomVisibility();
    }

    private void RefreshRoomVisibility()
    {
        var segmented = publicRoomButton != null ? publicRoomButton.GetComponentInParent<SegmentedPresentation>() : null;
        if (segmented != null) segmented.SetSelected(IsPublicRoom ? 0 : 1);
        SetVisibilityButton(publicRoomButton, IsPublicRoom);
        SetVisibilityButton(privateRoomButton, !IsPublicRoom);
        if (roomVisibilityText != null)
            roomVisibilityText.text = IsPublicRoom
                ? "공개방은 빠른 참가로 입장할 수 있어요."
                : "방 코드를 아는 사람만 입장할 수 있어요.";
    }

    private static void SetVisibilityButton(Button button, bool selected)
    {
        if (button == null) return;
        if (button.targetGraphic != null)
            button.targetGraphic.color = selected ? new Color32(145,95,48,255) : new Color(1,1,1,0);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = selected ? Color.white : new Color32(122,92,62,255);
    }

    protected override void OnOpen()
    {
        SetWindowsClosed();
        SetBusy(false);
    }

    protected override void OnClose() => SetWindowsClosed();

    private void SetWindowsClosed()
    {
        if (createRoomWindow != null) createRoomWindow.SetActive(false);
        if (joinRoomWindow != null) joinRoomWindow.SetActive(false);
    }

    public void OpenCreateRoomWindow()
    {
        if (busy || createRoomWindow == null) return;
        SetWindowsClosed();
        SetWindowStatus(createRoomStatusText, string.Empty);
        createRoomWindow.SetActive(true);
        RefreshRoomVisibility();
    }

    public void OpenJoinRoomWindow()
    {
        if (busy || joinRoomWindow == null) return;
        SetWindowsClosed();
        SetWindowStatus(joinRoomStatusText, string.Empty);
        joinRoomWindow.SetActive(true);
        RefreshJoinButton();
        roomCodeInput?.ActivateInputField();
        if (Application.isPlaying) StartCoroutine(FocusCodeInputAfterOpen());
    }

    private IEnumerator FocusCodeInputAfterOpen()
    {
        var group = joinRoomWindow.GetComponent<CanvasGroup>();
        while (joinRoomWindow.activeSelf && group != null && !group.interactable) yield return null;
        if (joinRoomWindow.activeSelf && !busy) roomCodeInput?.ActivateInputField();
    }

    private void CloseCreateRoomWindow() => CloseWindow(createRoomWindow);
    private void CloseJoinRoomWindow() => CloseWindow(joinRoomWindow);
    private void CloseWindow(GameObject window)
    {
        if (busy || window == null) return;
        var view = window.GetComponent<PopupPresentation>();
        if (Application.isPlaying && view != null) view.PlayClose(() => window.SetActive(false));
        else window.SetActive(false);
    }

    private void SetWindowStatus(TMP_Text text, string message)
    {
        if (text == null) return;
        text.text = message ?? string.Empty;
        text.color = busy ? new Color32(122,92,62,255) : new Color32(200,70,63,255);
        text.gameObject.SetActive(!string.IsNullOrEmpty(text.text));
    }

    private void OnRoomCodeChanged(string _) => RefreshJoinButton();
    private void RefreshJoinButton()
    {
        if (joinRoomButton != null)
            joinRoomButton.interactable = !busy && roomCodeInput != null && !string.IsNullOrWhiteSpace(roomCodeInput.text);
    }
    private void OnRoomCodeSubmitted(string _)
    {
        if (!busy && joinRoomButton != null && joinRoomButton.interactable) OnJoinRoomClicked();
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
