using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 아이콘 탭의 카드 한 장. 보유하면 원래 그림·이름, 미보유면 회색 그림 + "???"
public class IconCollectionItemView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    private static readonly Color OwnedColor = Color.white;
    private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    private const string LockedName = "???";

    private string iconName;   // 미보유 → 보유로 바뀔 때 되돌릴 실제 이름

    public void Init(ProfileIconDefinition definition)
    {
        iconName = definition.name;

        Sprite sprite = Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(definition.id));
        if (sprite == null)
            Debug.LogWarning($"[Collection] 아이콘 그림이 없습니다: {definition.id}", this);
        else
            iconImage.sprite = sprite;

        SetOwned(false);
    }

    public void SetOwned(bool owned)
    {
        iconImage.color = owned ? OwnedColor : LockedColor;
        nameText.text = owned ? iconName : LockedName;
    }
}
