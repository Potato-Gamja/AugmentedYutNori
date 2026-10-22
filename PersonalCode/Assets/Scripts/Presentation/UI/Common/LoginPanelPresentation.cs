using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutNori.UI.Common
{
    /// <summary>기존 LoginPanel 상태를 읽어 표시만 갱신한다. 로그인과 버튼 이벤트는 처리하지 않는다.</summary>
    [ExecuteAlways]
    public sealed class LoginPanelPresentation : MonoBehaviour
    {
        [SerializeField] private Button googleButton;
        [SerializeField] private CanvasGroup googleAppearance;
        [SerializeField] private Button continueButton;
        [SerializeField] private Image continueBorder;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text versionText;

        public void Configure(Button google, CanvasGroup appearance, Button resume, Image border,
            TMP_Text label, TMP_Text status, TMP_Text version)
        {
            googleButton = google;
            googleAppearance = appearance;
            continueButton = resume;
            continueBorder = border;
            continueLabel = label;
            statusText = status;
            versionText = version;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (googleButton != null && googleAppearance != null)
                googleAppearance.alpha = googleButton.IsInteractable() ? 1 : 0.45f;
            if (continueButton != null)
            {
                bool available = continueButton.IsInteractable();
                if (continueBorder != null)
                {
                    continueBorder.enabled = true;
                    continueBorder.color = available ? new Color32(183,121,63,255) : new Color32(220,198,160,255);
                }
                if (continueLabel != null)
                    continueLabel.color = available ? new Color32(58,38,24,255) : new Color32(185,172,151,255);
            }
            if (statusText != null)
            {
                statusText.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));
                bool firstLogin = googleButton != null && googleButton.IsInteractable() &&
                    continueButton != null && !continueButton.IsInteractable();
                statusText.color = firstLogin ? new Color32(168,142,110,255) : new Color32(122,92,62,255);
            }
            if (versionText != null) versionText.text = "v" + Application.version;
        }
    }
}
