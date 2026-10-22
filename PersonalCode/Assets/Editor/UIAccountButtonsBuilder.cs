using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 기존 패널을 다시 생성하지 않고 계정 버튼만 추가한다 (사용자가 넣은 Google 그림 보존).
public static class UIAccountButtonsBuilder
{
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static Button Button(Transform parent, string name, string label, float x, float y, float width, float height)
    {
        var existing = parent.Find(name);
        var root = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Common/SecondaryButton.prefab"), parent);
        root.name = name;
        root.SetActive(true);
        Top(root.GetComponent<RectTransform>(), x, y, width, height);
        var button = root.GetComponentInChildren<Button>(true);
        var text = button.transform.Find("Label").GetComponent<TMP_Text>();
        text.text = label;
        text.fontSize = height > 60 ? 28 : 24;
        text.rectTransform.sizeDelta = new Vector2(width - 20, height - 12);
        return button;
    }

    public static void ConfigureLogin(GameObject root)
    {
        var panel = root.GetComponent<LoginPanel>();
        var card = root.transform.Find("LoginCard");
        var rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, 400);
        var button = Button(card, "GuestButton", "게스트로 시작하기", 60, 248, 480, 76);
        UIPopupPrefabBuilder.Bind(panel, "guestLoginButton", button);
        var status = card.Find("Status").GetComponent<RectTransform>();
        status.anchoredPosition = new Vector2(300, -355);
        status.sizeDelta = new Vector2(540, 44);
    }

    public static void ConfigureProfile(GameObject root)
    {
        var panel = root.GetComponent<ProfilePanel>();
        var well = root.transform.Find("Popup/LeftWell");
        var logout = well.Find("Logout");
        Top(logout.GetComponent<RectTransform>(), 60, 528, 300, 72);
        var button = Button(well, "Withdraw", "탈퇴하기", 60, 620, 300, 52);
        UIPopupPrefabBuilder.Bind(panel, "withdrawButton", button);
    }

    [MenuItem("Tools/Yutnori/UIv2/Add Guest and Withdraw Buttons")]
    public static void Apply()
    {
        Modify("Assets/Prefabs/UIv2/Panels/LoginPanel.prefab", ConfigureLogin);
        Modify("Assets/Prefabs/UIv2/Popups/ProfilePanel.prefab", ConfigureProfile);
        var scenePath = "Assets/Scenes/Lobby.unity";
        var scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new System.InvalidOperationException("Lobby 씬의 변경을 먼저 저장해 주세요.");
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var coordinator in root.GetComponentsInChildren<LobbyNetworkCoordinator>(true))
            {
                var so = new SerializedObject(coordinator);
                so.FindProperty("allowGuestLogin").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
    }

    private static void Modify(string path, System.Action<GameObject> configure)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
