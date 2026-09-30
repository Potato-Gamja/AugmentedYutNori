using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI augumentName;
    [SerializeField] private TextMeshProUGUI augmentDescription;

    private Button selectButton;

    public void Init(string id, AugmentDataSO data, Action<string> onCardSelected)
    {
        augumentName.text = data != null ? data.AugmentName : id;
        augmentDescription.text = data != null ? data.AugmentDescription : "";

        selectButton = GetComponent<Button>();
        selectButton.onClick.AddListener(() => onCardSelected(id));
        
    }
}