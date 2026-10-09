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

    [Header("증강 도감")]
    [SerializeField] private TextAsset augmentsJson;
    [SerializeField] private AugmentCardView augmentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;

    private Button augmentTabButton;
    private Button achievementTabButton;
    private Button iconTabButton;
    private GameObject augmentPage;
    private GameObject achievementPage;
    private GameObject iconPage;

    private static readonly Color SelectedTabColor = new Color(0.12f, 0.58f, 0.36f, 1f);
    private static readonly Color NormalTabColor = new Color(0.88f, 0.82f, 0.68f, 1f);

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        BuildTabNavigation();
        BuildAugmentList();
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
            card.Init(definition, null);
        }
    }

    private void BuildTabNavigation()
    {
        ScrollRect augmentScroll = augmentCardContainer != null
            ? augmentCardContainer.GetComponentInParent<ScrollRect>()
            : null;
        augmentPage = augmentScroll != null ? augmentScroll.gameObject : augmentCardContainer?.gameObject;

        RectTransform tabBar = CreateRect("CollectionTabs", transform);
        tabBar.anchorMin = new Vector2(0.5f, 1f);
        tabBar.anchorMax = new Vector2(0.5f, 1f);
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -36f);
        tabBar.sizeDelta = new Vector2(660f, 64f);

        augmentTabButton = CreateTabButton(tabBar, "AugmentTab", "증강", -220f);
        achievementTabButton = CreateTabButton(tabBar, "AchievementTab", "업적", 0f);
        iconTabButton = CreateTabButton(tabBar, "IconTab", "아이콘", 220f);

        augmentTabButton.onClick.AddListener(OpenAugments);
        achievementTabButton.onClick.AddListener(OpenAchievements);
        iconTabButton.onClick.AddListener(OpenIcons);

        RectTransform augmentPageRect = augmentPage != null ? augmentPage.GetComponent<RectTransform>() : null;
        Image augmentPageBackground = augmentPage != null ? augmentPage.GetComponent<Image>() : null;
        achievementPage = CreatePlaceholderPage(
            "AchievementPage", "업적 목록은 준비 중입니다.", augmentPageRect, augmentPageBackground);
        iconPage = CreatePlaceholderPage(
            "IconPage", "아이콘 목록은 준비 중입니다.", augmentPageRect, augmentPageBackground);
    }

    private GameObject CreatePlaceholderPage(
        string objectName,
        string message,
        RectTransform referenceRect,
        Image referenceBackground)
    {
        // 기준 페이지와 부모가 다르면 같은 RectTransform 값도 화면에서 다른 크기와 위치가 된다.
        // 업적·아이콘 페이지를 증강 페이지와 같은 계층에 생성해 판넬 외형과 여백을 맞춘다.
        Transform pageParent = referenceRect != null && referenceRect.parent != null
            ? referenceRect.parent
            : transform;
        RectTransform page = CreateRect(objectName, pageParent);
        if (referenceRect != null)
        {
            // 증강 페이지를 기준으로 삼아 세 탭의 판넬 크기를 항상 동일하게 유지한다.
            page.anchorMin = referenceRect.anchorMin;
            page.anchorMax = referenceRect.anchorMax;
            page.pivot = referenceRect.pivot;
            page.anchoredPosition = referenceRect.anchoredPosition;
            page.sizeDelta = referenceRect.sizeDelta;
        }
        else
        {
            page.anchorMin = Vector2.zero;
            page.anchorMax = Vector2.one;
            page.offsetMin = new Vector2(50f, 50f);
            page.offsetMax = new Vector2(-50f, -150f);
        }

        Image background = page.gameObject.AddComponent<Image>();
        if (referenceBackground != null)
        {
            // 배경 스프라이트·색상·Image 타입도 증강 페이지와 같게 맞춘다.
            background.sprite = referenceBackground.sprite;
            background.color = referenceBackground.color;
            background.material = referenceBackground.material;
            background.type = referenceBackground.type;
            background.preserveAspect = referenceBackground.preserveAspect;
        }
        else
        {
            background.color = new Color(1f, 0.98f, 0.92f, 0.96f);
        }

        RectTransform labelRect = CreateRect("Message", page);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = message;
        label.fontSize = 36f;
        label.color = new Color(0.23f, 0.15f, 0.1f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return page.gameObject;
    }

    private static Button CreateTabButton(RectTransform parent, string objectName, string labelText, float x)
    {
        RectTransform buttonRect = CreateRect(objectName, parent);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(x, 0f);
        buttonRect.sizeDelta = new Vector2(200f, 56f);

        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = NormalTabColor;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform labelRect = CreateRect("Label", buttonRect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Normal;
        label.color = new Color(0.23f, 0.15f, 0.1f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return button;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void OpenAugments() => SetTab(CollectionTab.Augments);
    private void OpenAchievements() => SetTab(CollectionTab.Achievements);
    private void OpenIcons() => SetTab(CollectionTab.Icons);

    private void SetTab(CollectionTab tab)
    {
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
}
