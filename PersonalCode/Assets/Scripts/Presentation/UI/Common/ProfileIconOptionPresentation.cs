using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    /// <summary>기존 프로필 선택·보유 판정의 장식만 표시한다.</summary>
    public sealed class ProfileIconOptionPresentation : MonoBehaviour
    {
        [SerializeField] private Image selectedFrame, locked;
        [SerializeField] private TMP_Text stateText;
        public void Configure(Image frame, Image lockBadge, TMP_Text state){selectedFrame=frame;locked=lockBadge;stateText=state;}
        public void Show(bool selected, bool owned)
        {
            selectedFrame.gameObject.SetActive(selected);
            locked.gameObject.SetActive(!owned && !selected);
            stateText.color = selected ? new Color32(179,107,0,255) : owned ? new Color32(122,92,62,255) : new Color32(168,142,110,255);
        }
    }
}
