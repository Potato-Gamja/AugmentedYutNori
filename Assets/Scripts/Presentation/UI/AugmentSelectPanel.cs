using System;
using UnityEngine;

public class AugmentSelectPanel : UIBase
{
    [Header("UI 연결")]
    [SerializeField] private GameObject augumentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    // TODO: Augment 카드 수를 동적으로 설정할 수 있도록 변경 필요
    [SerializeField] private int numberOfAugmentCards = 3; // 표시할 Augment 카드 수

    
    private System.Collections.Generic.IReadOnlyList<string> choices;
private AugmentPool pool;
    private int team;
    private Action<string> onAugmentSelected;
    
public void Setup(AugmentPool pool, int team, Action<string> onAugmentSelected)
    {
        this.pool = pool;
        this.team = team;
        choices = null;
        this.onAugmentSelected = onAugmentSelected;
    }

    // GameRules가 만든 후보만 받아 표시하는 연결 API.
    // UI가 AugmentPool이나 중복 선택 규칙을 직접 알지 않도록 한다.
    public void Setup(System.Collections.Generic.IReadOnlyList<string> choices, Action<string> onAugmentSelected)
    {
        this.choices = choices;
        pool = null;
        this.onAugmentSelected = onAugmentSelected;
    }

protected override void OnOpen()
    {
        foreach (Transform child in augmentCardContainer) Destroy(child.gameObject);

        System.Collections.Generic.IEnumerable<string> ids = choices;
        if (ids == null && pool != null)
            ids = pool.GetRandomIds(team, numberOfAugmentCards);
        if (ids == null) return;

        foreach (string id in ids)
        {
            AugmentCardView card = Instantiate(augumentCardViewPrefab, augmentCardContainer).GetComponent<AugmentCardView>();
            card.Init(id, OnCardSelected);
        }
    }

    private void OnCardSelected(string id)
    {
        Close();
        onAugmentSelected?.Invoke(id);
    }
}