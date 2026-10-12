using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로그인 화면 입력과 표시만 담당한다. 실제 로그인은 LoginController가 처리한다.
/// </summary>
public class LoginPanel : UIBase
{
    [Header("버튼")]
    [SerializeField] private Button googleLoginButton;
    // 이 기기에 저장된 로그인으로 바로 들어간다. 저장된 로그인이 있을 때만 누를 수 있다
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    // 로그인 진행 중에만 보인다 (계속하기 버튼 자리). 브라우저 창을 그냥 닫았을 때 기다림을 끝내는 용도
    [SerializeField] private Button cancelButton;

    [Header("상태")]
    [SerializeField] private TMP_Text statusText;

    private bool busy;
    private bool continueAvailable;

    // 버튼을 누르는 순간 (WebGL 팝업은 이 시점에 준비해야 한다)
    public event Action GoogleLoginPressed;
    // 버튼 클릭 (누르고 뗐을 때)
    public event Action GoogleLoginRequested;
    public event Action ContinueRequested;
    public event Action CancelRequested;

    public override void Init()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
            cancelButton.gameObject.SetActive(false);
        }

        continueButton?.onClick.AddListener(OnContinueClicked);
        SetContinueAvailable(false, null);

        if (googleLoginButton == null) return;

        googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);

        var trigger = googleLoginButton.GetComponent<EventTrigger>() ?? googleLoginButton.gameObject.AddComponent<EventTrigger>();
        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(OnGoogleLoginPointerDown);
        trigger.triggers.Add(pointerDown);
    }

    private void OnDestroy()
    {
        googleLoginButton?.onClick.RemoveListener(OnGoogleLoginClicked);
        continueButton?.onClick.RemoveListener(OnContinueClicked);
        cancelButton?.onClick.RemoveListener(OnCancelClicked);
    }

    public void SetBusy(bool isBusy)
    {
        busy = isBusy;
        if (googleLoginButton != null) googleLoginButton.interactable = !busy;
        RefreshContinue();
    }

    // 저장된 로그인이 있으면 계속하기 버튼을 켠다. nickname이 있으면 버튼에 같이 보여 준다
    public void SetContinueAvailable(bool available, string nickname)
    {
        continueAvailable = available;
        if (continueLabel != null)
        {
            continueLabel.text = available && !string.IsNullOrWhiteSpace(nickname)
                ? $"{nickname}(으)로 계속하기"
                : "이전 로그인으로 계속하기";
        }
        RefreshContinue();
    }

    // 로그인 진행 중이면 계속하기 대신 취소 버튼을 보여 준다
    public void SetCancelable(bool cancelable)
    {
        if (cancelButton != null) cancelButton.gameObject.SetActive(cancelable);
        if (continueButton != null) continueButton.gameObject.SetActive(!cancelable);
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private void RefreshContinue()
    {
        if (continueButton != null) continueButton.interactable = continueAvailable && !busy;
    }

    private void OnGoogleLoginPointerDown(BaseEventData _)
    {
        if (googleLoginButton != null && googleLoginButton.interactable) GoogleLoginPressed?.Invoke();
    }

    private void OnGoogleLoginClicked()
    {
        GoogleLoginRequested?.Invoke();
    }

    private void OnContinueClicked()
    {
        ContinueRequested?.Invoke();
    }

    private void OnCancelClicked()
    {
        CancelRequested?.Invoke();
    }
}
