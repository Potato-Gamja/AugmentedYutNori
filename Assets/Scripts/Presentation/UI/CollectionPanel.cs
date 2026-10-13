using UnityEngine;
using UnityEngine.UI;

/// <summary>등록된 증강의 아이콘, 이름과 설명을 보여 주는 화면.</summary>
public class CollectionPanel : UIBase
{
    [SerializeField] private Button closeButton;

    [Header("증강 목록")]
    [SerializeField] private TextAsset augmentsJson;
    [SerializeField] private AugmentCardView augmentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        BuildAugmentList();
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
    }

    private void BuildAugmentList()
    {
        if (augmentsJson == null || augmentCardViewPrefab == null || augmentCardContainer == null)
        {
            Debug.LogWarning("[Collection] 증강 데이터 또는 UI 참조가 없습니다.", this);
            return;
        }

        AugmentDefinitionFile file = JsonUtility.FromJson<AugmentDefinitionFile>(augmentsJson.text);
        AugmentCatalog catalog = new AugmentCatalog(file, AugmentFactories.Create());

        foreach (AugmentDefinition definition in catalog.Definitions)
        {
            AugmentCardView card = Instantiate(augmentCardViewPrefab, augmentCardContainer);
            card.Init(definition);
        }
    }
}
