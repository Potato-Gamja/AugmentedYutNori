using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardView : MonoBehaviour
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

    private Button selectButton;

    public void Init(AugmentDefinition definition, Action<string> onCardSelected)
    {
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
}
