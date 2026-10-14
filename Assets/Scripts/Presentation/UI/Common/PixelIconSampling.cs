using UnityEngine;
using UnityEngine.UI;

namespace YutNori.UI.Common
{
    /// <summary>표시 중 픽셀 아이콘의 샘플링만 Point로 맞춘다. Importer 설정은 변경하지 않는다.</summary>
    [ExecuteAlways]
    public sealed class PixelIconSampling : MonoBehaviour
    {
        private Image image;
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (image == null) image = GetComponent<Image>();
            if (image != null && image.sprite != null && image.sprite.texture.filterMode != FilterMode.Point)
                image.sprite.texture.filterMode = FilterMode.Point;
        }
    }
}
