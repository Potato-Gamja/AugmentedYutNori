using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SettingsUiAssembler
{
    private const string SettingsPrefabPath = "Assets/Prefabs/UI/SettingsPanel.prefab";
    private static readonly Color Paper = new(1f, 0.976f, 0.91f, 1f);
    private static readonly Color Brown = new(0.28f, 0.18f, 0.11f, 1f);
    private static readonly Color Green = new(0.12f, 0.58f, 0.36f, 1f);

    [MenuItem("Tools/Yutnori/Assemble Settings UI")]
    public static void Assemble()
    {
        CreateSettingsPrefab();
        AddSettingsButton("Assets/Prefabs/UI/GameHudPanel_3Col.prefab", "GameSettingsButton", "설정");
        AddSettingsButton("Assets/Prefabs/UI/LobbyPanel.prefab", "LobbySettingsButton", "설정");
        RegisterSettingsPrefab();
        RegisterSettingsInScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SettingsUI] 설정 패널, 로비/게임 버튼, UIRoot 등록을 완료했습니다.");
    }

    private static void CreateSettingsPrefab()
    {
        GameObject root = UiObject("SettingsPanel", null);
        Stretch(root.GetComponent<RectTransform>());
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0.08f, 0.055f, 0.035f, 0.62f);
        dim.raycastTarget = true;
        SettingsPanel panel = root.AddComponent<SettingsPanel>();

        GameObject card = UiObject("Card", root.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        Center(cardRect, new Vector2(620f, 480f), Vector2.zero);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = Paper;

        CreateText("Title", card.transform, "설정", 38f, FontStyles.Bold,
            new Vector2(0f, 170f), new Vector2(520f, 60f));
        CreateText("Description", card.transform, "소리 크기를 조절할 수 있습니다.", 20f, FontStyles.Normal,
            new Vector2(0f, 125f), new Vector2(520f, 40f));

        Slider master = CreateSlider(card.transform, "MasterSlider", "전체 음량", 65f, out TMP_Text masterValue);
        Slider bgm = CreateSlider(card.transform, "BgmSlider", "배경 음악", -25f, out TMP_Text bgmValue);
        Slider sfx = CreateSlider(card.transform, "SfxSlider", "효과음", -115f, out TMP_Text sfxValue);

        Button close = CreateButton(card.transform, "CloseButton", "닫기", new Vector2(0f, -190f), new Vector2(220f, 58f));

        SerializedObject serialized = new(panel);
        serialized.FindProperty("layer").enumValueIndex = (int)UILayer.Popup;
        serialized.FindProperty("masterSlider").objectReferenceValue = master;
        serialized.FindProperty("bgmSlider").objectReferenceValue = bgm;
        serialized.FindProperty("sfxSlider").objectReferenceValue = sfx;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.FindProperty("masterValueText").objectReferenceValue = masterValue;
        serialized.FindProperty("bgmValueText").objectReferenceValue = bgmValue;
        serialized.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void AddSettingsButton(string prefabPath, string buttonName, string label)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform existing = FindDeep(root.transform, buttonName);
            Button button;
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
            else
            {
                button = CreateButton(root.transform, buttonName, label, Vector2.zero, new Vector2(104f, 48f));
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(104f, 48f);
            rect.anchoredPosition = new Vector2(-22f, -20f);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            Component owner = root.GetComponent(prefabPath.Contains("Lobby") ? typeof(LobbyPanel) : typeof(GameHudPanel));
            SerializedObject serialized = new(owner);
            serialized.FindProperty("settingsButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RegisterSettingsPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/UIRoot.prefab");
        try
        {
            UIManager manager = root.GetComponentInChildren<UIManager>(true);
            SettingsPanel settings = AssetDatabase.LoadAssetAtPath<SettingsPanel>(SettingsPrefabPath);
            SerializedObject serialized = new(manager);
            SerializedProperty prefabs = serialized.FindProperty("prefabs");
            for (int i = 0; i < prefabs.arraySize; i++)
                if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == settings) return;
            prefabs.InsertArrayElementAtIndex(prefabs.arraySize);
            prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/UIRoot.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RegisterSettingsInScenes()
    {
        string activePath = SceneManager.GetActiveScene().path;
        foreach (string scenePath in new[] { "Assets/Scenes/Game_2.unity", "Assets/Scenes/Lobby.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            UIManager manager = Object.FindFirstObjectByType<UIManager>();
            if (manager == null) continue;

            SettingsPanel settings = AssetDatabase.LoadAssetAtPath<SettingsPanel>(SettingsPrefabPath);
            SerializedObject serialized = new(manager);
            SerializedProperty prefabs = serialized.FindProperty("prefabs");
            bool exists = false;
            for (int i = 0; i < prefabs.arraySize; i++)
                if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == settings) exists = true;
            if (!exists)
            {
                prefabs.InsertArrayElementAtIndex(prefabs.arraySize);
                prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = settings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!string.IsNullOrEmpty(activePath))
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
    }

    private static Slider CreateSlider(Transform parent, string name, string title, float y, out TMP_Text valueText)
    {
        CreateText(name + "Label", parent, title, 24f, FontStyles.Bold, new Vector2(-190f, y + 28f), new Vector2(220f, 40f));
        valueText = CreateText(name + "Value", parent, "100%", 22f, FontStyles.Bold, new Vector2(225f, y + 28f), new Vector2(90f, 40f));

        GameObject sliderObject = UiObject(name, parent);
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        Center(rect, new Vector2(440f, 38f), new Vector2(45f, y - 10f));
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject background = UiObject("Background", sliderObject.transform);
        Stretch(background.GetComponent<RectTransform>(), new Vector2(0f, 12f));
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.78f, 0.69f, 0.55f, 1f);

        GameObject fillArea = UiObject("Fill Area", sliderObject.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(8f, 12f));
        GameObject fill = UiObject("Fill", fillArea.transform);
        Stretch(fill.GetComponent<RectTransform>());
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = Green;

        GameObject handleArea = UiObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea.GetComponent<RectTransform>(), new Vector2(12f, 0f));
        GameObject handle = UiObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Center(handleRect, new Vector2(30f, 30f), Vector2.zero);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = UiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        Center(rect, size, position);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Green;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.18f, 0.68f, 0.44f, 1f);
        colors.pressedColor = new Color(0.08f, 0.42f, 0.25f, 1f);
        button.colors = colors;
        TMP_Text label = CreateText("Label", buttonObject.transform, text, 23f, FontStyles.Bold, Vector2.zero, size);
        label.color = Color.white;
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, FontStyles style, Vector2 position, Vector2 dimensions)
    {
        GameObject textObject = UiObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Center(rect, dimensions, position);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = Brown;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.layer = LayerMask.NameToLayer("UI");
        if (parent != null) result.transform.SetParent(parent, false);
        return result;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, Vector2 inset = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }
}
