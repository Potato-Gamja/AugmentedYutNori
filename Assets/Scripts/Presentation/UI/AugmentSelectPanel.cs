using System;
using System.Collections.Generic;
using UnityEngine;

public class AugmentSelectPanel : UIBase
{
    [Header("UI 연결")]
    [SerializeField] private GameObject augumentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;
    
    private IReadOnlyList<AugmentDefinition> choices;
    private Action<string> onAugmentSelected;

    // GameRules가 만든 후보만 받아 표시하는 연결 API.
    // UI가 AugmentPool이나 중복 선택 규칙을 직접 알지 않도록 한다.
    public void Setup(IReadOnlyList<AugmentDefinition> choices, Action<string> onAugmentSelected)
    {
        this.choices = choices;
        this.onAugmentSelected = onAugmentSelected;
    }

    protected override void OnOpen()
    {
        foreach (Transform child in augmentCardContainer) Destroy(child.gameObject);

        if (choices == null) return;
        foreach (AugmentDefinition choice in choices)
        {
            AugmentCardView card = Instantiate(augumentCardViewPrefab, augmentCardContainer)
                .GetComponent<AugmentCardView>();
            card.Init(choice, OnCardSelected);
        }
    }

    private void OnCardSelected(string id)
    {
        Close();
        onAugmentSelected?.Invoke(id);
    }
}