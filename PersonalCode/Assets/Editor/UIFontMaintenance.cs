using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>새 UI용 Dynamic 폰트 3종만 정리한다. Static 글자와 폴백은 변경하지 않는다.</summary>
public static class UIFontMaintenance
{
    private static readonly string[] DynamicPaths =
    {
        UIScreenPreparation.JuaDynamic,
        UIScreenPreparation.MediumDynamic,
        UIScreenPreparation.Bold
    };

    [MenuItem("Tools/Yutnori/Fonts/Clear UI Dynamic Font Data")]
    public static void ClearDynamicData()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play 모드를 종료한 뒤 폰트 데이터를 비워 주세요.");

        var fonts = DynamicPaths.Select(LoadDynamic).ToArray();
        var report = new List<string>();
        foreach (var font in fonts)
        {
            int before = font.characterTable.Count;
            var serialized = new SerializedObject(font);
            var clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild == null)
                throw new InvalidOperationException("Clear Dynamic Data On Build 필드 없음: " + font.name);
            clearOnBuild.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 최소 크기의 빈 아틀라스로 저장한다. 다음 표시 시 원본에서 2048 아틀라스를 다시 만든다.
            font.ClearFontAssetData(true);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            if (font.characterTable.Count != 0 || font.glyphTable.Count != 0)
                throw new InvalidOperationException("Dynamic 데이터가 다시 채워졌습니다: " + font.name);
            report.Add(font.name + ": characters " + before + " -> 0, glyphs 0, Clear On Build=True");
        }
        Directory.CreateDirectory(UIScreenPreparation.Reports);
        File.WriteAllText(UIScreenPreparation.Reports + "dynamic_font_clear.txt", string.Join("\n", report) + "\n");
        Debug.Log("[UI fonts] " + string.Join("\n", report));
    }

    [MenuItem("Tools/Yutnori/Fonts/Validate New UI Font References")]
    public static string ValidateReferences()
    {
        var brokenStatic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Medium);
        var checkedTexts = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UIv2" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                checkedTexts++;
                if (text.font == brokenStatic)
                    throw new InvalidOperationException("깨진 Medium Static 직접 참조: " + path + "/" + text.name);
            }
        }
        foreach (var path in DynamicPaths)
        {
            var font = LoadDynamic(path);
            var serialized = new SerializedObject(font);
            if (!serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue)
                throw new InvalidOperationException("Clear Dynamic Data On Build가 꺼져 있습니다: " + path);
            if (!font.isMultiAtlasTexturesEnabled || font.atlasWidth != 2048 || font.atlasHeight != 2048 ||
                font.atlasPadding != 10 || font.faceInfo.pointSize != 48 ||
                font.atlasRenderMode != UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA)
                throw new InvalidOperationException("Dynamic 설정 변경: " + path);
        }
        string report = "UIv2 TMP texts checked: " + checkedTexts + "\nBroken Medium Static direct references: 0\nDynamic 3 settings and Clear On Build: PASS\n";
        Directory.CreateDirectory(UIScreenPreparation.Reports);
        File.WriteAllText(UIScreenPreparation.Reports + "dynamic_font_validation.txt", report);
        Debug.Log(report);
        return report;
    }

    private static TMP_FontAsset LoadDynamic(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic || font.sourceFontFile == null)
            throw new InvalidOperationException("원본 폰트가 연결된 Dynamic 에셋이 아닙니다: " + path);
        return font;
    }
}
