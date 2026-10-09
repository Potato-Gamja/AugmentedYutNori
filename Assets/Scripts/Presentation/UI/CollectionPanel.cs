using UnityEngine;
using UnityEngine.UI;

public class CollectionPanel : UIBase
{
    [SerializeField] private Button closeButton;

    [Header("증강 도감")]
    [SerializeField] private TextAsset augmentsJson;
    [SerializeField] private AugmentCardView augmentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
        BuildAugmentList();
    }

    private void OnDestroy()
    {
        closeButton.onClick.RemoveListener(Close);
    }

    private void BuildAugmentList()
    {
        AugmentDefinitionFile file = JsonUtility.FromJson<AugmentDefinitionFile>(augmentsJson.text);
        AugmentCatalog catalog = new AugmentCatalog(file, AugmentFactories.Create());

        foreach (AugmentDefinition definition in catalog.Definitions)
        {
            AugmentCardView card = Instantiate(augmentCardViewPrefab, augmentCardContainer);
            card.Init(definition, null);
        }
    }
}
