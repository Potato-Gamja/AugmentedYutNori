using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutNori.UI.Common
{
    /// <summary>RoomPanel이 결정한 상태의 모양만 바꾼다. 참가·준비 판정은 하지 않는다.</summary>
    public sealed class RoomSlotPresentation : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject shadow;
        [SerializeField] private Image statusPill;
        [SerializeField] private GameObject check;
        [SerializeField] private TMP_Text nameText, statusText;
        [SerializeField] private TMP_FontAsset bold, medium;

        public void Configure(CanvasGroup canvasGroup, GameObject cardShadow, Image pill, GameObject checkIcon,
            TMP_Text nickname, TMP_Text status, TMP_FontAsset boldFont, TMP_FontAsset mediumFont)
        { group=canvasGroup;shadow=cardShadow;statusPill=pill;check=checkIcon;nameText=nickname;statusText=status;bold=boldFont;medium=mediumFont; }

        public void Show(bool occupied, bool ready, bool reconnecting)
        {
            group.alpha=occupied?1:0.7f;
            shadow.SetActive(occupied);
            nameText.fontSize=occupied?36:34;
            nameText.fontSizeMax=occupied?36:34;
            nameText.color=occupied?new Color32(58,38,24,255):new Color32(168,142,110,255);
            bool checkedReady=occupied&&ready&&!reconnecting;
            statusPill.enabled=occupied;
            statusPill.color=checkedReady?new Color32(58,38,24,255):new Color32(243,230,207,255);
            check.SetActive(checkedReady);
            statusText.font=occupied?bold:medium;
            statusText.fontSize=occupied?21:20;
            statusText.color=reconnecting?new Color32(164,108,43,255):checkedReady?Color.white:
                occupied?new Color32(122,92,62,255):new Color32(168,142,110,255);
            // 긴 재연결 문구도 카드 안에서 한 줄로 읽히도록 알약 폭만 늘린다.
            float width=Mathf.Max(176,statusText.preferredWidth+(checkedReady?60:40));
            statusPill.rectTransform.sizeDelta=new Vector2(Mathf.Min(340,width),46);
            statusText.rectTransform.offsetMin=new Vector2(checkedReady?36:12,0);
            statusText.rectTransform.offsetMax=new Vector2(-12,0);
        }
    }
}
