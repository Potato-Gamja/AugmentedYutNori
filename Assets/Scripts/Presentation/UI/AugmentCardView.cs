using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI augumentName;
    [SerializeField] private TextMeshProUGUI augmentDescription;

    private Button selectButton;

    public void Init(AugmentDefinition definition, Action<string> onCardSelected)
    {
        augumentName.text = definition.name;
        augmentDescription.text = definition.description;

        selectButton = GetComponent<Button>();
        if (onCardSelected == null)
        {
            if (selectButton != null) selectButton.interactable = false;
            return;
        }
        selectButton.onClick.AddListener(() => onCardSelected(definition.id));
    }
}