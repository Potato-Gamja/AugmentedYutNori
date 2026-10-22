using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    public sealed class InputFocusPresentation:MonoBehaviour,ISelectHandler,IDeselectHandler
    {
        [SerializeField] private Image border;
        public void Configure(Image target){border=target;SetFocused(false);}
        public void SetFocused(bool focused){if(border!=null)border.enabled=focused;}
        private void OnDisable()=>SetFocused(false);
        public void OnSelect(BaseEventData e)=>SetFocused(true);
        public void OnDeselect(BaseEventData e)=>SetFocused(false);
    }
}
