using System;
using UnityEngine;

public class AugmentSelectPanel : UIBase
{
    [Header("UI 연결")]
    [SerializeField] private GameObject augumentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    // TODO: Augment 카드 수를 동적으로 설정할 수 있도록 변경 필요
    [SerializeField] private int numberOfAugmentCards = 3; // 표시할 Augment 카드 수

    private AugmentPool pool;
    private int team;
    private Action<string> onAugmentSelected;
    
    public void Setup(AugmentPool pool, int team, Action<string> onAugmentSelected)
    {
        this.pool = pool;
        this.team = team;
        this.onAugmentSelected = onAugmentSelected;
    }

    protected override void OnOpen()
    {
        foreach (Transform child in augmentCardContainer) Destroy(child.gameObject);
        foreach (string id in pool.GetRandomIds(team, numberOfAugmentCards))
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