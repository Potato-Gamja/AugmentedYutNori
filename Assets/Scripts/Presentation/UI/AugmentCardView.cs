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
        selectButton.onClick.AddListener(() => onCardSelected(definition.id));
    }
}