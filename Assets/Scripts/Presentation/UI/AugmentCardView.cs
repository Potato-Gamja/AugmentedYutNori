using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AugmentCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly string[] AugmentIds =
    {
        "Assassin", "Hunter", "Marathoner", "Monk", "Sprinter", "Loner",
        "Pacifist", "FriendShield", "Gambler", "Rethrow", "Contrarian"
    };

    [SerializeField] private Image augmentIcon;
    [SerializeField] private TextMeshProUGUI augumentName;
    [SerializeField] private TextMeshProUGUI augmentDescription;
    [SerializeField] private List<Sprite> augmentIcons = new();
    [Header("표시")]
    [SerializeField] private RectTransform visual;
    [SerializeField] private Image categoryPill;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private Image hoverBorder;
    [SerializeField] private Image selectedBorder;
    [SerializeField] private Image selectedGlow;
    [SerializeField] private Image checkBadge;
    [SerializeField] private Image checkMark;
    [SerializeField] private Sprite[] teamCheckBadges;

    private Button selectButton;
    private bool hovered, selected;
    public string AugmentId { get; private set; }

    public static Color TeamColor(int team, bool dark = false)
    {
        string[] colors = dark ? new[] { "922A26", "1E478F", "1C6642", "8F5C00" }
            : new[] { "D6403C", "2E6BD1", "2A9460", "E0950F" };
        ColorUtility.TryParseHtmlString("#" + colors[Mathf.Clamp(team, 0, 3)], out Color color);
        return color;
    }

    public void Init(AugmentDefinition definition, Action<string> onCardSelected = null)
    {
        AugmentId = definition.id;
        hovered = false;
        SetSelected(false);
        ShowCategory(definition.id);
        if (augmentIcon != null)
        {
            int iconIndex = Array.IndexOf(AugmentIds, definition.id);
            augmentIcon.sprite = iconIndex >= 0 && iconIndex < augmentIcons.Count
                ? augmentIcons[iconIndex]
                : null;
            augmentIcon.enabled = augmentIcon.sprite != null;
            augmentIcon.preserveAspect = true;
        }

        augumentName.text = definition.name;
        augmentDescription.text = definition.description;

        selectButton = GetComponent<Button>();
        if (selectButton != null) selectButton.onClick.RemoveAllListeners();
        if (onCardSelected == null)
        {
            if (selectButton != null) selectButton.interactable = false;
            return;
        }
        selectButton.onClick.AddListener(() => onCardSelected(definition.id));
        SoundManager.BindButtonClick(selectButton);
    }

    private void ShowCategory(string id)
    {
        if (categoryPill == null || categoryLabel == null) return;
        string category, color;
        switch (id)
        {
            case "Assassin": case "Hunter": category = "공격"; color = "94584E"; break;
            case "Marathoner": case "Monk": case "Sprinter": category = "이동"; color = "6B8A6E"; break;
            case "Loner": case "Pacifist": case "FriendShield": category = "방어"; color = "5B7A8C"; break;
            case "Gambler": case "Rethrow": case "Contrarian": category = "운"; color = "8C6BA8"; break;
            default: category = string.Empty; color = "A88E6E"; break;
        }
        categoryLabel.text = category;
        categoryPill.rectTransform.sizeDelta = new Vector2(categoryLabel.preferredWidth + 36, categoryPill.rectTransform.sizeDelta.y);
        ColorUtility.TryParseHtmlString("#" + color, out Color tint);
        categoryPill.color = tint;
        categoryPill.gameObject.SetActive(category.Length > 0);
    }

    public void SetTeam(int team)
    {
        Color color = TeamColor(team);
        if (selectedBorder != null) selectedBorder.color = color;
        if (selectedGlow != null) { color.a = 0.5f; selectedGlow.color = color; }
        if (checkBadge != null && teamCheckBadges != null && teamCheckBadges.Length == 4)
            checkBadge.sprite = teamCheckBadges[Mathf.Clamp(team, 0, 3)];
        if (checkMark != null) checkMark.color = team == 3 ? new Color32(58, 38, 24, 255) : Color.white;
    }

    public void SetInput(bool enabled)
    {
        if (selectButton != null) selectButton.interactable = enabled;
        if (!enabled) SetHovered(false);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (selectedBorder != null) selectedBorder.gameObject.SetActive(value);
        if (selectedGlow != null) selectedGlow.gameObject.SetActive(value);
        if (checkBadge != null) checkBadge.gameObject.SetActive(value);
        if (hoverBorder != null) hoverBorder.gameObject.SetActive(!value && hovered);
    }

    public void SetHovered(bool value)
    {
        hovered = value;
        if (hoverBorder != null) hoverBorder.gameObject.SetActive(value && !selected);
    }

    public void OnPointerEnter(PointerEventData eventData)
    { if (selectButton != null && selectButton.IsInteractable()) SetHovered(true); }
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    private void Update()
    {
        if (visual == null) return;
        Vector3 position = visual.localPosition;
        position.y = Mathf.MoveTowards(position.y, hovered || selected ? 8 : 0, 80 * Time.unscaledDeltaTime);
        visual.localPosition = position;
    }
}
