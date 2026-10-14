using UnityEngine;

namespace YutNori.UI.Common
{
    /// <summary>장식 말의 표시용 움직임만 처리한다.</summary>
    public sealed class DecorativeFloat : MonoBehaviour
    {
        [SerializeField] private float phase;
        private RectTransform rect;
        private Vector2 origin;

        public void Configure(float offset) => phase = offset;
        private void OnEnable()
        {
            rect = GetComponent<RectTransform>();
            origin = rect.anchoredPosition;
        }
        private void Update()
        {
            rect.anchoredPosition = origin + Vector2.up *
                (Mathf.Sin(Time.unscaledTime * Mathf.PI + phase) * 6);
        }
        private void OnDisable()
        {
            if (rect != null) rect.anchoredPosition = origin;
        }
    }
}
