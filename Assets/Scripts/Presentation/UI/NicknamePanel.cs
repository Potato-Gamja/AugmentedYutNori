using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 계정 닉네임 입력 화면 (첫 설정·변경 공용). 입력 검사와 표시만 하고, 서버 저장은 이 패널을 연 쪽이 처리한다.
/// </summary>
public class NicknamePanel : UIBase
{
    // 서버 Player.nickname 칼럼 길이와 같게
    public const int MinLength = 2;
    public const int MaxLength = 20;

    [Header("입력")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("버튼")]
    [SerializeField] private Button confirmButton;

    [Header("상태")]
    [SerializeField] private TMP_Text statusText;

    // 검사를 통과한 닉네임 (앞뒤 공백 제거)
    public event Action<string> Submitted;

    public override void Init()
    {
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = MaxLength;
            nicknameInput.onSubmit.AddListener(OnInputSubmitted);
        }
    }

    private void OnDestroy()
    {
        confirmButton?.onClick.RemoveListener(OnConfirmClicked);
        nicknameInput?.onSubmit.RemoveListener(OnInputSubmitted);
    }

    protected override void OnOpen()
    {
        ShowStatus(string.Empty);
        SetBusy(false);
        nicknameInput?.ActivateInputField();
    }

    // 변경 화면으로 열 때 지금 닉네임을 미리 채운다
    public void SetNickname(string nickname)
    {
        if (nicknameInput != null) nicknameInput.text = nickname ?? string.Empty;
    }

    public void SetBusy(bool busy)
    {
        if (confirmButton != null) confirmButton.interactable = !busy;
        if (nicknameInput != null) nicknameInput.interactable = !busy;
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    // 빈 값·길이만 검사한다. 중복 여부는 서버가 409로 알려 준다
    public static string Validate(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "닉네임을 입력해 주세요.";
        int length = nickname.Trim().Length;
        if (length < MinLength || length > MaxLength) return $"닉네임은 {MinLength}~{MaxLength}자로 입력해 주세요.";
        return null;
    }

    private void OnInputSubmitted(string _) => OnConfirmClicked();

    private void OnConfirmClicked()
    {
        if (confirmButton != null && !confirmButton.interactable) return;

        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        string error = Validate(nickname);
        if (error != null)
        {
            ShowStatus(error);
            return;
        }

        Submitted?.Invoke(nickname);
    }
}
