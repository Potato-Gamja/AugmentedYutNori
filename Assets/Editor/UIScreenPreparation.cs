using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>명세 0-2 준비. 기존 Static은 폴백 목록만 변경한다.</summary>
public static class UIScreenPreparation
{
    public const string Folder = "Assets/TextMesh Pro/Fonts/";
    public const string Reports = "Docs/UI/Reports/";
    public const string Jua = Folder + "Jua-Regular SDF.asset";
    public const string Medium = Folder + "Pretendard-Medium SDF.asset";
    public const string JuaDynamic = Folder + "Jua-Regular Dynamic SDF.asset";
    public const string MediumDynamic = Folder + "Pretendard-Medium Dynamic SDF.asset";
    public const string Bold = Folder + "Pretendard-Bold SDF.asset";
    private static TMP_FontAsset Load(string path) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
        ?? throw new InvalidOperationException("Font Asset 없음: " + path);

    [MenuItem("Tools/Yutnori/Screens/Prepare Stage 1 Fonts")]
    public static void PrepareFonts()
    {
        var jua = Load(Jua); var medium = Load(Medium);
        string juaBefore = StaticSignature(jua), mediumBefore = StaticSignature(medium);
        var jd = CreateDynamic(JuaDynamic, "Assets/Fonts/Jua/Jua-Regular.ttf");
        var md = CreateDynamic(MediumDynamic, "Assets/Fonts/Pretendard/Pretendard-Medium.otf");
        var bold = CreateDynamic(Bold, "Assets/Fonts/Pretendard/Pretendard-Bold.otf");
        jua.fallbackFontAssetTable = new List<TMP_FontAsset> { jd, md };
        medium.fallbackFontAssetTable = new List<TMP_FontAsset> { md };
        bold.fallbackFontAssetTable = new List<TMP_FontAsset> { md };
        EditorUtility.SetDirty(jua); EditorUtility.SetDirty(medium); EditorUtility.SetDirty(bold);
        AssetDatabase.SaveAssets();
        if (StaticSignature(jua) != juaBefore || StaticSignature(medium) != mediumBefore)
            throw new InvalidOperationException("Static 글자·아틀라스 변경 감지");
        Debug.Log("[Screens] Dynamic 3개와 폴백 연결 완료. Static 글자·아틀라스 보존 확인.");
    }
    private static TMP_FontAsset CreateDynamic(string path, string sourcePath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null)
        {
            if (asset.atlasPopulationMode != AtlasPopulationMode.Dynamic || !asset.isMultiAtlasTexturesEnabled ||
                asset.atlasWidth != 2048 || asset.atlasHeight != 2048 || asset.atlasPadding != 10 ||
                asset.atlasRenderMode != GlyphRenderMode.SDFAA || asset.faceInfo.pointSize != 48 ||
                AssetDatabase.GetAssetPath(asset.sourceFontFile) != sourcePath)
                throw new InvalidOperationException("기존 Dynamic 설정 확인 필요: " + path);
            return asset;
        }
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null) throw new InvalidOperationException("Font 파일 없음: " + sourcePath);
        asset = TMP_FontAsset.CreateFontAsset(source, 48, 10, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (asset == null) throw new InvalidOperationException("Font 생성 실패: " + sourcePath);
        asset.name = Path.GetFileNameWithoutExtension(path);
        asset.creationSettings = new FontAssetCreationSettings { sourceFontFileGUID = AssetDatabase.AssetPathToGUID(sourcePath),
            pointSizeSamplingMode = 1, pointSize = 48, padding = 10, paddingMode = 2,
            atlasWidth = 2048, atlasHeight = 2048, renderMode = (int)GlyphRenderMode.SDFAA };
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures)
        { texture.name = asset.name + " Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
        EditorUtility.SetDirty(asset);
        return asset;
    }
    public static string StaticSignature(TMP_FontAsset font)
    {
        var data = new StringBuilder(JsonUtility.ToJson(font.faceInfo));
        data.Append(font.atlasPopulationMode).Append(font.atlasWidth).Append(font.atlasHeight).Append(font.atlasPadding).Append(font.atlasRenderMode);
        foreach (var glyph in font.glyphTable)
            data.Append(glyph.index).Append(JsonUtility.ToJson(glyph.metrics)).Append(JsonUtility.ToJson(glyph.glyphRect));
        foreach (var character in font.characterTable) data.Append(character.unicode).Append(':').Append(character.glyphIndex);
        foreach (var texture in font.atlasTextures) data.Append(Hash(texture.GetRawTextureData()));
        return Hash(Encoding.UTF8.GetBytes(data.ToString()));
    }
    private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
    [MenuItem("Tools/Yutnori/Screens/Validate Stage 1 Fonts")]
    public static void ValidateFonts()
    {
        var report = new StringBuilder("# Stage 1 font validation\n\n");
        foreach (var path in new[] { Jua, Medium, Bold })
        {
            var font = Load(path);
            const string sample = "윷놀이 준비 증강 선택 재연결 대기 중 닉네임 똠힣 · × → ‹ … 2";
            string missing = new string(sample.Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c, true, true)).Distinct().ToArray());
            if (missing.Length != 0) throw new InvalidOperationException(path + " 표시 못한 글자: " + missing);
            report.AppendLine(path + ": glyph sample PASS");
        }
        foreach (var path in new[] { JuaDynamic, MediumDynamic, Bold })
        {
            var font = Load(path);
            report.AppendLine($"{path}: {font.atlasPopulationMode}, MultiAtlas={font.isMultiAtlasTexturesEnabled}, {font.atlasWidth}×{font.atlasHeight}, Sampling={font.faceInfo.pointSize}, Padding={font.atlasPadding}, {font.atlasRenderMode}");
            foreach (var texture in font.atlasTextures) if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
        }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory(Reports);
        File.WriteAllText(Reports + "stage1_fonts.txt", report.ToString(), new UTF8Encoding(false));
        Debug.Log(report.ToString());
    }
    public static void CaptureGameHud(string fileName, bool symbols = false, bool originalFallbacks = false)
    {
        var juaFont = Load(Jua); var mediumFont = Load(Medium);
        var juaFallbacks = juaFont.fallbackFontAssetTable; var mediumFallbacks = mediumFont.fallbackFontAssetTable;
        if (originalFallbacks)
        {
            juaFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
            mediumFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null; Texture2D picture = null; var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("PreviewCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene); camera.orthographic = true; camera.orthographicSize = 540;
            camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(244, 231, 203, 255); camera.cullingMask = 1 << 30;
            var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            var hud = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameHudPanel_3Col.prefab"), canvasObject.transform, false);
            hud.GetComponent<GameHudPanel>().Init(); hud.SetActive(true);
            if (symbols)
            {
                var obj = new GameObject("SymbolSample", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(canvasObject.transform, false);
                var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1500, 70); rect.anchoredPosition = new Vector2(0, -490);
                var text = obj.GetComponent<TextMeshProUGUI>(); text.font = Load(Jua); text.fontSize = 34;
                text.alignment = TextAlignmentOptions.Center; text.color = new Color32(58, 38, 24, 255);
                text.text = "윷놀이 · 증강 선택 · 업힌 말 ×2 → 도착 … 닉네임 똠힣";
            }
            foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            Canvas.ForceUpdateCanvases(); foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            target = new RenderTexture(1920, 1080, 24); target.Create(); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            picture = new Texture2D(1920, 1080, TextureFormat.RGB24, false); picture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); picture.Apply();
            Directory.CreateDirectory(Reports); File.WriteAllBytes(Reports + fileName, picture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (picture != null) UnityEngine.Object.DestroyImmediate(picture);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
            juaFont.fallbackFontAssetTable = juaFallbacks; mediumFont.fallbackFontAssetTable = mediumFallbacks;
        }
    }
}
