using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>10연차 결과 화면의 칸 하나. 아이콘·이름·NEW 표시를 채운다. (GachaResultView가 사용)</summary>
public sealed class GachaResultSlot : MonoBehaviour
{
    private static readonly Color NewColor = new Color32(255, 236, 178, 255);
    private static readonly Color DuplicateColor = new Color32(243, 230, 207, 255);

    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject newBadge;
    [SerializeField] private TMP_Text duplicateText;

    public RectTransform IconRoot => iconRoot;

    public void Show(GachaDrawResult result, string iconName, Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = iconName ?? result.iconId;
        if (background != null) background.color = result.duplicate ? DuplicateColor : NewColor;
        if (newBadge != null) newBadge.SetActive(!result.duplicate);
        if (duplicateText != null)
        {
            duplicateText.gameObject.SetActive(result.duplicate);
            duplicateText.text = $"x{result.count}";
        }
    }
}
