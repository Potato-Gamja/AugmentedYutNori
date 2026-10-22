using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YutNori.UI.Common
{
    public sealed class MenuCardInteraction : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private Vector2 origin;
        private bool hovered;
        private bool pressed;
        private Coroutine motion;
        private void OnEnable() { rect = GetComponent<RectTransform>(); origin = rect.anchoredPosition; }
        private void OnDisable()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null; hovered = pressed = false;
            if (rect != null) rect.anchoredPosition = origin;
        }
        public void OnPointerEnter(PointerEventData _) { hovered = true; Move(); }
        public void OnPointerExit(PointerEventData _) { hovered = pressed = false; Move(); }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = true; Move(); } }
        public void OnPointerUp(PointerEventData _) { pressed = false; Move(); }
        private void Move()
        {
            var target = origin + Vector2.up * (pressed ? -2 : hovered ? 4 : 0);
            if (motion != null) StopCoroutine(motion);
            if (Application.isPlaying) motion = StartCoroutine(Animate(target));
            else rect.anchoredPosition = target;
        }
        private IEnumerator Animate(Vector2 target)
        {
            var start = rect.anchoredPosition;
            for (float t = 0; t < 0.08f; t += Time.unscaledDeltaTime)
            { rect.anchoredPosition = Vector2.Lerp(start,target,Mathf.Clamp01(t/0.08f)); yield return null; }
            rect.anchoredPosition = target; motion = null;
        }
    }
}
