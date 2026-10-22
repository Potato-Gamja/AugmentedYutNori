using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>기존 UIv2 구조를 유지하고 노장 아이콘만 추가한다. 전용 그림이 오면 다시 실행한다.</summary>
public static class VeteranUIIntegration
{
    public static Sprite Icon
    {
        get
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == "augveteran" || name == "aug_veteran")
                    return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            }
            return Resources.Load<Sprite>("ProfileIcons/icon_010");
        }
    }

    [MenuItem("Tools/Yutnori/Augments/Apply Veteran UI Icon")]
    public static void Apply()
    {
        Sprite icon = Icon;
        if (icon == null) throw new InvalidOperationException("노장 그림과 임시 icon_010이 없습니다.");
        foreach (string path in new[]
        {
            "Assets/Prefabs/UIv2/Items/AugmentCardView.prefab",
            "Assets/Prefabs/UIv2/Items/AugmentCardView_Compact.prefab",
            "Assets/Prefabs/UI/GameHudPanel_3Col.prefab"
        })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Component component = (Component)root.GetComponent<AugmentCardView>() ?? root.GetComponent<GameHudPanel>();
                var so = new SerializedObject(component);
                var icons = so.FindProperty("augmentIcons");
                icons.arraySize = 12;
                icons.GetArrayElementAtIndex(11).objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        const string collection = "Assets/Prefabs/UIv2/Popups/CollectionPanel.prefab";
        GameObject popup = PrefabUtility.LoadPrefabContents(collection);
        try
        {
            foreach (TMPro.TMP_Text text in popup.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.text == "직업 11종의 효과를 확인하세요") text.text = "직업 12종의 효과를 확인하세요";
            PrefabUtility.SaveAsPrefabAsset(popup, collection);
        }
        finally { PrefabUtility.UnloadPrefabContents(popup); }
    }
}
