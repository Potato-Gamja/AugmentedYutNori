using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI augumentName;
    [SerializeField] private TextMeshProUGUI augmentDescription;

    private Button selectButton;

    public void Init(string id, Action<string> onCardSelected)
    {
        augumentName.text = id;

        selectButton = GetComponent<Button>();
        selectButton.onClick.AddListener(() => onCardSelected(id));
        
    }
}