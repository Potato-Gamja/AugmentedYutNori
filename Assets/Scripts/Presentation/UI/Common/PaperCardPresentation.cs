using UnityEngine;
namespace YutNori.UI.Common
{
    public sealed class PaperCardPresentation:MonoBehaviour
    {
        [SerializeField] private GameObject glow,border;
        public void Configure(GameObject highlight,GameObject outline){glow=highlight;border=outline;SetEmphasis(false,false);}
        public void SetSize(Vector2 size)=>((RectTransform)transform).sizeDelta=size;
        public void SetEmphasis(bool highlight,bool outline){if(glow!=null)glow.SetActive(highlight);if(border!=null)border.SetActive(outline);}
    }
}
