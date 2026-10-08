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
    // 로그인 진행 중에만 보인다. 브라우저 창을 그냥 닫았을 때 기다림을 끝내는 용도
    [SerializeField] private Button cancelButton;

    [Header("상태")]
    [SerializeField] private TMP_Text statusText;

    // 버튼을 누르는 순간 (WebGL 팝업은 이 시점에 준비해야 한다)
    public event Action GoogleLoginPressed;
    // 버튼 클릭 (누르고 뗐을 때)
    public event Action GoogleLoginRequested;
    public event Action CancelRequested;

    public override void Init()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
            cancelButton.gameObject.SetActive(false);
        }

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
        cancelButton?.onClick.RemoveListener(OnCancelClicked);
    }

    public void SetBusy(bool busy)
    {
        if (googleLoginButton != null) googleLoginButton.interactable = !busy;
    }

    // 로그인 진행 중이면 취소 버튼을 보여 준다
    public void SetCancelable(bool cancelable)
    {
        if (cancelButton != null) cancelButton.gameObject.SetActive(cancelable);
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private void OnGoogleLoginPointerDown(BaseEventData _)
    {
        if (googleLoginButton != null && googleLoginButton.interactable) GoogleLoginPressed?.Invoke();
    }

    private void OnGoogleLoginClicked()
    {
        GoogleLoginRequested?.Invoke();
    }

    private void OnCancelClicked()
    {
        CancelRequested?.Invoke();
    }
}
