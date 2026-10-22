using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    [ExecuteAlways] public sealed class ButtonPresentation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image face, border;
        [SerializeField] private TMP_Text label, subtitle;
        [SerializeField] private GameObject glow;
        [SerializeField] private bool primary, glowVisible;
        [SerializeField] private bool overrideLabelColor = true;
        private bool pressed;
        public void Configure(Button target, Image surface, Image outline, TMP_Text title, TMP_Text sub, GameObject highlight, bool isPrimary)
        { button=target;face=surface;border=outline;label=title;subtitle=sub;glow=highlight;primary=isPrimary;Refresh(); }
        public void SetLabelColorOverride(bool enabled) {overrideLabelColor=enabled;Refresh();}
        public void SetGlowVisible(bool visible) {glowVisible=visible;Refresh();}
        public void SetSubtitle(string value) {if(subtitle!=null){subtitle.text=value;subtitle.gameObject.SetActive(!string.IsNullOrEmpty(value));}Refresh();}
        private void OnEnable(){pressed=false;Refresh();}
        private void Update()=>Refresh();
        public void Refresh()
        {
            if(button==null||face==null)return;
            bool active=button.IsInteractable();if(!active)pressed=false;
            face.color=primary&&active?new Color32(145,95,48,255):Color.white;
            if(label!=null){if(overrideLabelColor)label.color=!active?new Color32(168,154,132,255):primary?Color.white:new Color32(58,38,24,255);
                label.rectTransform.anchoredPosition=new Vector2(0,(subtitle!=null&&subtitle.gameObject.activeSelf?10:0)-(pressed?4:0));}
            if(subtitle!=null){subtitle.color=active?new Color32(255,240,225,255):new Color32(168,154,132,255);subtitle.rectTransform.anchoredPosition=new Vector2(0,-25-(pressed?4:0));}
            if(border!=null)border.enabled=active;
            if(glow!=null)glow.SetActive(active&&glowVisible);
        }
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button!=null&&button.IsInteractable()){pressed=true;Refresh();}}
        public void OnPointerUp(PointerEventData e){pressed=false;Refresh();}
        public void OnPointerExit(PointerEventData e){pressed=false;Refresh();}
    }
}
