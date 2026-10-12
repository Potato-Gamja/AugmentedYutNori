using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Resources/ProfileIcons의 PNG 목록으로 Data/profile_icons.json을 맞춘다.
//   Tools/Yutnori/Profile Icons/아이콘 json 동기화 : 없는 PNG는 json에 추가(이름 = id), 이미 있는 항목의 이름과 순서는 그대로
//                                           PNG가 없는데 json에 남은 id는 지우지 않고 경고만 한다
// 아이콘 추가 순서: PNG를 Resources/ProfileIcons에 넣기 → 이 메뉴 실행 → json에서 name만 고치기
// 서버 profile-icons.json에도 같은 id를 넣어야 한다 (아직 수동)
public static class ProfileIconSync
{
    private const string MenuRoot = "Tools/Yutnori/Profile Icons/";
    private const string IconFolder = "Assets/Resources/" + ProfileIconCatalog.SpriteFolder;
    private const string JsonPath = "Assets/Data/profile_icons.json";

    [MenuItem(MenuRoot + "아이콘 json 동기화")]
    private static void Sync()
    {
        List<string> pngIds = FindPngIds();
        ProfileIconDefinitionFile file = LoadJson();
        var icons = new List<ProfileIconDefinition>(file.icons ?? new ProfileIconDefinition[0]);
        var jsonIds = new HashSet<string>(icons.Select(icon => icon.id));

        List<string> added = pngIds.Where(id => !jsonIds.Contains(id)).ToList();
        List<string> missingPng = icons.Select(icon => icon.id).Where(id => !pngIds.Contains(id)).ToList();

        foreach (string id in added)
        {
            icons.Add(new ProfileIconDefinition { id = id, name = id });
        }

        List<string> fixedImports = FixImportSettings(pngIds);

        foreach (string id in missingPng)
        {
            Debug.LogWarning($"[ProfileIconSync] '{id}'는 json에 있지만 {IconFolder}에 PNG가 없습니다. 필요 없으면 json에서 지우세요.");
        }

        if (added.Count == 0)
        {
            Debug.Log($"[ProfileIconSync] 추가할 아이콘이 없습니다. (PNG {pngIds.Count}개, json {icons.Count}개)");
            return;
        }

        file.version = file.version <= 0 ? 1 : file.version;
        file.icons = icons.ToArray();
        File.WriteAllText(JsonPath, ToJson(file), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(JsonPath);

        string importNote = fixedImports.Count > 0 ? $" (Sprite로 임포트 설정: {string.Join(", ", fixedImports)})" : string.Empty;
        Debug.Log($"[ProfileIconSync] json에 {added.Count}개 추가: {string.Join(", ", added)}{importNote}. name을 실제 이름으로 고쳐 주세요.",
            AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath));
    }

    private static List<string> FindPngIds()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder)) return new List<string>();

        return AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() == ".png")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToList();
    }

    private static ProfileIconDefinitionFile LoadJson()
    {
        if (!File.Exists(JsonPath)) return new ProfileIconDefinitionFile { version = 1 };

        return JsonUtility.FromJson<ProfileIconDefinitionFile>(File.ReadAllText(JsonPath))
               ?? new ProfileIconDefinitionFile { version = 1 };
    }

    // 새로 넣은 PNG가 Sprite가 아니면 UI Image에 못 쓰므로 맞춰 준다
    private static List<string> FixImportSettings(List<string> ids)
    {
        var fixedIds = new List<string>();
        foreach (string id in ids)
        {
            string path = $"{IconFolder}/{id}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            if (importer.textureType == TextureImporterType.Sprite) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            fixedIds.Add(id);
        }
        return fixedIds;
    }

    // augments.json처럼 아이콘 하나를 한 줄로 쓴다
    private static string ToJson(ProfileIconDefinitionFile file)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"version\": {file.version},\n");
        sb.Append("  \"icons\": [\n");
        for (int i = 0; i < file.icons.Length; i++)
        {
            ProfileIconDefinition icon = file.icons[i];
            sb.Append($"    {{ \"id\": \"{Escape(icon.id)}\", \"name\": \"{Escape(icon.name)}\" }}");
            sb.Append(i < file.icons.Length - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}
