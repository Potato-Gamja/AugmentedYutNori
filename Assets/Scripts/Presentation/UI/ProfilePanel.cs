using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>저장된 계정 프로필을 표시한다. 서버 프로필 API가 추가되면 ShowProfile 입력만 교체한다.</summary>
public sealed class ProfilePanel : UIBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Image profileIcon;
    [SerializeField] private TMP_Text iconPlaceholder;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button saveNicknameButton;

    public event Action<string> NicknameChangeRequested;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        saveNicknameButton?.onClick.AddListener(OnSaveNicknameClicked);
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
        saveNicknameButton?.onClick.RemoveListener(OnSaveNicknameClicked);
    }

    public void ShowProfile(string playerId, string nickname, Sprite icon = null)
    {
        if (nicknameText != null)
            nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? "닉네임 미설정" : nickname;
        if (nicknameInput != null)
            nicknameInput.SetTextWithoutNotify(nickname ?? string.Empty);
        if (playerIdText != null)
            playerIdText.text = string.IsNullOrWhiteSpace(playerId) ? "플레이어 ID 없음" : $"플레이어 ID  {playerId}";
        if (profileIcon != null && icon != null) profileIcon.sprite = icon;
        if (iconPlaceholder != null) iconPlaceholder.gameObject.SetActive(icon == null);
        if (statusText != null)
            statusText.text = icon == null ? "프로필 아이콘은 서버 연동 후 표시됩니다." : string.Empty;
    }

    public void SetNicknameBusy(bool busy)
    {
        if (nicknameInput != null) nicknameInput.interactable = !busy;
        if (saveNicknameButton != null) saveNicknameButton.interactable = !busy;
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    public void ApplyNickname(string nickname)
    {
        if (nicknameText != null) nicknameText.text = nickname;
        if (nicknameInput != null) nicknameInput.SetTextWithoutNotify(nickname ?? string.Empty);
    }

    private void OnSaveNicknameClicked()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        if (nickname.Length < 2 || nickname.Length > 20)
        {
            ShowStatus("닉네임은 2~20자로 입력해 주세요.");
            return;
        }

        NicknameChangeRequested?.Invoke(nickname);
    }
}
