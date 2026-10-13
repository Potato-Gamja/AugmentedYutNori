using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionPanel : UIBase
{
    private enum CollectionTab
    {
        Augments,
        Achievements,
        Icons,
    }

    [SerializeField] private Button closeButton;

    [Header("탭")]
    [SerializeField] private Button augmentTabButton;
    [SerializeField] private Button achievementTabButton;
    [SerializeField] private Button iconTabButton;
    [SerializeField] private GameObject augmentPage;
    [SerializeField] private GameObject achievementPage;
    [SerializeField] private GameObject iconPage;

    [Header("증강 도감")]
    [SerializeField] private TextAsset augmentsJson;
    [SerializeField] private AugmentCardView augmentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    [Header("아이콘 도감")]
    [SerializeField] private TextAsset profileIconsJson;
    [SerializeField] private IconCollectionItemView iconItemPrefab;
    [SerializeField] private Transform iconItemContainer;
    [SerializeField] private TMP_Text iconsStatusText;

    private static readonly Color SelectedTabColor = new Color(0.12f, 0.58f, 0.36f, 1f);
    private static readonly Color NormalTabColor = new Color(0.88f, 0.82f, 0.68f, 1f);

    private readonly Dictionary<string, IconCollectionItemView> iconItems = new();

    private CollectionTab currentTab;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        augmentTabButton?.onClick.AddListener(OpenAugments);
        achievementTabButton?.onClick.AddListener(OpenAchievements);
        iconTabButton?.onClick.AddListener(OpenIcons);
        BuildAugmentList();
        BuildIconList();
        SetTab(CollectionTab.Augments);
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
        augmentTabButton?.onClick.RemoveListener(OpenAugments);
        achievementTabButton?.onClick.RemoveListener(OpenAchievements);
        iconTabButton?.onClick.RemoveListener(OpenIcons);
    }

    private void BuildAugmentList()
    {
        if (augmentsJson == null || augmentCardViewPrefab == null || augmentCardContainer == null)
        {
            Debug.LogWarning("[Collection] 증강 도감 데이터 또는 UI 참조가 없습니다.", this);
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

    private void BuildIconList()
    {
        if (profileIconsJson == null || iconItemPrefab == null || iconItemContainer == null)
        {
            Debug.LogWarning("[Collection] 아이콘 도감 데이터 또는 UI 참조가 없습니다.", this);
            return;
        }

        ProfileIconCatalog catalog = new ProfileIconCatalog(
            JsonUtility.FromJson<ProfileIconDefinitionFile>(profileIconsJson.text));

        foreach (ProfileIconDefinition definition in catalog.Definitions)
        {
            IconCollectionItemView item = Instantiate(iconItemPrefab, iconItemContainer);
            item.Init(definition);
            iconItems[definition.id] = item;
        }
    }

    private void OpenAugments() => SetTab(CollectionTab.Augments);
    private void OpenAchievements() => SetTab(CollectionTab.Achievements);
    private void OpenIcons()
    {
        SetTab(CollectionTab.Icons);
        RefreshOwnedIcons();
    }

    protected override void OnOpen()
    {
        if (currentTab == CollectionTab.Icons) RefreshOwnedIcons();
    }

    private void SetTab(CollectionTab tab)
    {
        currentTab = tab;

        if (augmentPage != null) augmentPage.SetActive(tab == CollectionTab.Augments);
        if (achievementPage != null) achievementPage.SetActive(tab == CollectionTab.Achievements);
        if (iconPage != null) iconPage.SetActive(tab == CollectionTab.Icons);

        SetTabSelected(augmentTabButton, tab == CollectionTab.Augments);
        SetTabSelected(achievementTabButton, tab == CollectionTab.Achievements);
        SetTabSelected(iconTabButton, tab == CollectionTab.Icons);
    }

    private static void SetTabSelected(Button button, bool selected)
    {
        if (button?.targetGraphic is Image image)
            image.color = selected ? SelectedTabColor : NormalTabColor;

        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
        if (label != null) label.color = selected ? Color.white : new Color(0.23f, 0.15f, 0.1f, 1f);
    }

    private async void RefreshOwnedIcons()
    {
        ShowIconsStatus(string.Empty);
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator == null)
        {
            ShowIconsStatus("로비에서 열어야 보유 아이콘을 볼 수 있습니다.");
            return;
        }
        try
        {
            OwnedIconsResponse response = await coordinator.GetOwnedIconsAsync();
            if (this == null || !IsOpen) return;

            foreach (var item in iconItems.Values) item.SetOwned(false);
            foreach (string id in response.iconIds)
            {
                if (iconItems.TryGetValue(id, out var item))
                    item.SetOwned(true);
            }
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[Collection] 보유 아이콘 조회 실패: {e.Message}", this);
            if (this != null)
                ShowIconsStatus("보유 아이콘을 불러오지 못했습니다.");
        }
    }

    private void ShowIconsStatus(string message)
    {
        if (iconsStatusText == null) return;
        iconsStatusText.text = message;
        iconsStatusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}
