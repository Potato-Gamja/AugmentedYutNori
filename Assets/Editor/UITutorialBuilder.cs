using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutNori.UI.Common;

public static class UITutorialBuilder
{
    public const string Path = "Assets/Prefabs/UIv2/Popups/TutorialPanel.prefab";
    private static GameObject Common(string name, Transform parent) => (GameObject)PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Common/" + name + ".prefab"), parent);
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
    private static TMP_Text Text(Transform parent, string name, string text, float x, float y, float width, float height, bool title)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        Top(go.GetComponent<RectTransform>(), x, y, width, height);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(title ? UIScreenPreparation.Jua : UIScreenPreparation.MediumDynamic);
        label.fontSize = title ? 40 : 28;
        label.color = new Color32(58, 38, 24, 255);
        label.text = text; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.lineSpacing = 8;
        return label;
    }
    private static Button Action(Transform parent, string name, string text, float x, float y)
    {
        var root = Common("SecondaryButton", parent); root.name = name;
        Top(root.GetComponent<RectTransform>(), x, y, 260, 72);
        var button = root.GetComponentInChildren<Button>(true);
        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.text = text; label.fontSize = 30; label.rectTransform.sizeDelta = new Vector2(240, 60);
        return button;
    }
    public static void ConfigureMenu(GameObject root)
    {
        var existing = root.transform.Find("TutorialButton");
        var go = existing != null ? existing.gameObject : Common("SecondaryButton", root.transform);
        go.name = "TutorialButton";
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(-144, -76); rect.sizeDelta = new Vector2(180, 64);
        var button = go.GetComponentInChildren<Button>(true);
        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.text = "게임 방법"; label.fontSize = 26; label.rectTransform.sizeDelta = new Vector2(160, 52);
        go.GetComponentInChildren<ButtonPresentation>(true).SetLabelColorOverride(false);
        UIPopupPrefabBuilder.Bind(root.GetComponent<MainMenuPanel>(), "tutorialButton", button);
        // 설정 옆에 버튼 자리를 만들고 골드와 겹치지 않게 한다.
        var gold = root.transform.Find("Gold").GetComponent<RectTransform>();
        gold.anchoredPosition = new Vector2(-390, gold.anchoredPosition.y);
    }
    private static Sprite Screenshot(string name)
    {
        string path = "Assets/Art/UI/Tutorial/" + name + ".png";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("튜토리얼 그림 없음: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("Tools/Yutnori/UIv2/Rebuild Tutorial With Screenshots")]
    public static void Generate()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        var images = new[] { Screenshot("Room"), Screenshot("Augments_Select"),
            Screenshot("MainGame_YutThrow"), Screenshot("MainGame_Move") };
        var root = Common("PopupFrame", null);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "TutorialPanel";
            var panel = root.AddComponent<TutorialPanel>();
            var so = new SerializedObject(panel); so.FindProperty("layer").enumValueIndex = (int)UILayer.Popup; so.ApplyModifiedPropertiesWithoutUndo();
            var body = root.transform.Find("Popup").GetComponent<RectTransform>(); body.sizeDelta = new Vector2(1660, 940);
            body.Find("Title").GetComponent<TMP_Text>().text = "게임 방법";
            body.Find("Title").GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 60);
            body.Find("Description").gameObject.SetActive(false); body.Find("Gold").gameObject.SetActive(false);
            UIPopupPrefabBuilder.Bind(panel, "closeButton", body.Find("CloseButton").GetComponentInChildren<Button>(true));
            UIPopupPrefabBuilder.Bind(panel, "pageTitle", Text(body, "PageTitle", "01  대기실에서 준비하기", 64, 132, 1500, 64, true));
            var frame = new GameObject("ScreenshotFrame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(body, false);
            Top(frame.GetComponent<RectTransform>(), 56, 220, 960, 556);
            var well = frame.GetComponent<Image>(); well.sprite = UIPopupPrefabBuilder.Sprite("slot_well");
            well.type = Image.Type.Sliced; well.raycastTarget = false;
            var shot = new GameObject("Screenshot", typeof(RectTransform), typeof(Image));
            shot.transform.SetParent(frame.transform, false);
            Top(shot.GetComponent<RectTransform>(), 12, 12, 936, 532);
            var image = shot.GetComponent<Image>(); image.sprite = images[0]; image.preserveAspect = true; image.raycastTarget = false;
            UIPopupPrefabBuilder.Bind(panel, "pageImage", image);
            var serialized = new SerializedObject(panel); var pages = serialized.FindProperty("pageImages"); pages.arraySize = 5;
            for (int i = 0; i < 5; i++) pages.GetArrayElementAtIndex(i).objectReferenceValue = images[Mathf.Min(i, 3)];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var text = Text(body, "Body", "", 1060, 230, 536, 550, false);
            text.fontSize = 28; text.lineSpacing = 6;
            UIPopupPrefabBuilder.Bind(panel, "bodyText", text);
            var caption = Text(body, "ImageCaption", "", 64, 790, 944, 42, false);
            caption.fontSize = 22; caption.color = new Color32(122, 92, 62, 255);
            UIPopupPrefabBuilder.Bind(panel, "imageCaption", caption);
            var count = Text(body, "PageNumber", "1 / 5", 750, 858, 160, 50, false); count.alignment = TextAlignmentOptions.Center;
            UIPopupPrefabBuilder.Bind(panel, "pageNumber", count);
            UIPopupPrefabBuilder.Bind(panel, "previousButton", Action(body, "Previous", "이전", 64, 842));
            var next = Action(body, "Next", "다음", 1336, 842);
            UIPopupPrefabBuilder.Bind(panel, "nextButton", next);
            UIPopupPrefabBuilder.Bind(panel, "nextButtonText", next.transform.Find("Label").GetComponent<TMP_Text>());
            root.GetComponent<PopupPresentation>().Configure(body, root.GetComponent<CanvasGroup>(), null);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/Yutnori/UIv2/Add Lobby Tutorial")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Lobby 씬의 변경을 먼저 저장해 주세요.");
        Generate();
        const string menuPath = "Assets/Prefabs/UIv2/Panels/MainMenuPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(menuPath);
        try { ConfigureMenu(root); PrefabUtility.SaveAsPrefabAsset(root, menuPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Additive);
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<TutorialPanel>();
            foreach (var owner in scene.GetRootGameObjects())
            foreach (var manager in owner.GetComponentsInChildren<UIManager>(true))
            {
                var so = new SerializedObject(manager); var list = so.FindProperty("prefabs"); int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is TutorialPanel) { index = i; break; }
                if (index < 0) { index = list.arraySize; list.arraySize++; }
                list.GetArrayElementAtIndex(index).objectReferenceValue = prefab; so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
    }
}
