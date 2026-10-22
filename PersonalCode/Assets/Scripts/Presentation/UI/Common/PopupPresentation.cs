using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    public sealed class PopupPresentation:MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button closeButton;
        private Coroutine animation;
        private bool closing;
        public void Configure(RectTransform body,CanvasGroup canvasGroup,Button close){panel=body;group=canvasGroup;closeButton=close;}
        private void OnEnable(){closing=false;if(closeButton!=null)closeButton.onClick.AddListener(Close);if(Application.isPlaying&&panel!=null&&group!=null)animation=StartCoroutine(Animate(true,null));}
        private void OnDisable(){if(closeButton!=null)closeButton.onClick.RemoveListener(Close);if(animation!=null)StopCoroutine(animation);animation=null;closing=false;if(panel!=null)panel.localScale=Vector3.one;if(group!=null){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}}
        public void Close()=>PlayClose(()=>gameObject.SetActive(false));
        public void PlayClose(Action completed){if(closing||!isActiveAndEnabled)return;closing=true;if(animation!=null)StopCoroutine(animation);animation=StartCoroutine(Animate(false,completed));}
        public void SetSize(Vector2 size){if(panel!=null)panel.sizeDelta=size;}
        private IEnumerator Animate(bool opening,Action completed)
        {
            group.interactable=false;group.blocksRaycasts=true;
            float duration=opening?0.15f:0.1f,from=opening?0:group.alpha;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float p=Mathf.Clamp01(t/duration);group.alpha=Mathf.Lerp(from,opening?1:0,p);panel.localScale=Vector3.one*Mathf.Lerp(opening?0.92f:1,opening?1:0.92f,p);yield return null;}
            group.alpha=opening?1:0;panel.localScale=Vector3.one*(opening?1:0.92f);group.interactable=opening;animation=null;completed?.Invoke();
        }
    }
}
