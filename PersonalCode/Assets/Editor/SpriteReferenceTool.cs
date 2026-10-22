using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬·프리팹 등 텍스트 직렬화 파일 안의 스프라이트 참조(fileID + guid)를 찾고, 바꾸고, 검사한다.
/// .meta 파일은 읽거나 쓰지 않는다.
/// </summary>
public static class SpriteReferenceTool
{
    public const long SingleSpriteFileId = 21300000;
    private const long TextureFileId = 2800000;

    private const string VerifyMenuPath = "Tools/Yutnori/Verify Sprite References";

    private static readonly string[] YamlExtensions =
    {
        ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".overrideController", ".spriteatlas",
    };

    // 이 작업에서 수정하지 않기로 한 파일. 바꿔야 할 참조가 있으면 해당 PNG 변환 자체를 보류한다.
    private static readonly string[] ProtectedPaths =
    {
        "Assets/Scenes/LSU_Game_UI.unity",
        "Assets/Prefabs/UI/Tests/",
    };

    private static readonly Regex ReferencePattern =
        new Regex(@"fileID: (-?\d+), guid: ([0-9a-f]{32})", RegexOptions.Compiled);

    public static bool IsProtected(string assetPath) =>
        ProtectedPaths.Any(p => p.EndsWith("/")
            ? assetPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)
            : string.Equals(assetPath, p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Assets 아래 텍스트 직렬화 파일 경로 목록.
    /// </summary>
    public static List<string> FindYamlFiles()
    {
        return Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories)
            .Where(f => YamlExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(ToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 파일별로 (guid, fileID) 참조 개수를 센다. guids에 든 것만 센다.
    /// </summary>
    public static Dictionary<string, Dictionary<(string guid, long fileId), int>> CountReferences(
        IEnumerable<string> yamlFiles, ICollection<string> guids)
    {
        var result = new Dictionary<string, Dictionary<(string, long), int>>();
        foreach (string file in yamlFiles)
        {
            string text = File.ReadAllText(file);
            foreach (Match match in ReferencePattern.Matches(text))
            {
                string guid = match.Groups[2].Value;
                if (!guids.Contains(guid)) continue;

                long fileId = long.Parse(match.Groups[1].Value);
                if (!result.TryGetValue(file, out var counts))
                {
                    counts = new Dictionary<(string, long), int>();
                    result.Add(file, counts);
                }
                counts.TryGetValue((guid, fileId), out int count);
                counts[(guid, fileId)] = count + 1;
            }
        }
        return result;
    }

    /// <summary>
    /// 파일 안의 "fileID: old, guid: g"를 "fileID: new, guid: g"로 바꾸고 바꾼 개수를 돌려준다.
    /// </summary>
    public static int RewriteReferences(string assetPath, IReadOnlyDictionary<string, long> oldFileIdByGuid)
    {
        string text = File.ReadAllText(assetPath);
        int replaced = 0;
        string rewritten = ReferencePattern.Replace(text, match =>
        {
            string guid = match.Groups[2].Value;
            long fileId = long.Parse(match.Groups[1].Value);
            if (!oldFileIdByGuid.TryGetValue(guid, out long oldId) || fileId != oldId) return match.Value;

            replaced++;
            return $"fileID: {SingleSpriteFileId}, guid: {guid}";
        });

        if (replaced > 0) File.WriteAllText(assetPath, rewritten, new UTF8Encoding(false));
        return replaced;
    }

    /// <summary>
    /// 텍스처의 스프라이트 하위 에셋 (이름, fileID) 목록.
    /// </summary>
    public static List<(string name, long fileId)> GetSpriteIds(string texturePath)
    {
        var ids = new List<(string, long)>();
        foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>())
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId))
                ids.Add((sprite.name, localId));
        }
        return ids;
    }

    [MenuItem(VerifyMenuPath)]
    public static void VerifyMenu()
    {
        Verify(FindYamlFiles(), out string message);
        const string reportPath = "Logs/sprite_reference_report.txt";
        File.WriteAllText(reportPath, $"# Verify Sprite References {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{message}\n", new UTF8Encoding(false));
        Debug.Log($"[SpriteRef] 보고서 저장: {reportPath}");
    }

    /// <summary>
    /// Assets 아래 PNG를 가리키는 참조 중, 그 PNG에 없는 fileID를 가리키는 것(Missing)을 찾는다.
    /// </summary>
    public static int Verify(IEnumerable<string> yamlFiles, out string message)
    {
        var validIdsByGuid = new Dictionary<string, HashSet<long>>();
        var pathByGuid = new Dictionary<string, string>();
        var missingByFile = new Dictionary<string, List<string>>();
        int checkedReferences = 0;

        foreach (string file in yamlFiles)
        {
            var counts = new Dictionary<string, int>();
            foreach (Match match in ReferencePattern.Matches(File.ReadAllText(file)))
            {
                string guid = match.Groups[2].Value;
                if (!pathByGuid.TryGetValue(guid, out string texturePath))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    texturePath = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? path : null;
                    pathByGuid.Add(guid, texturePath);
                }
                if (texturePath == null) continue;

                if (!validIdsByGuid.TryGetValue(guid, out var validIds))
                {
                    validIds = new HashSet<long>(GetSpriteIds(texturePath).Select(s => s.fileId)) { TextureFileId };
                    validIdsByGuid.Add(guid, validIds);
                }

                checkedReferences++;
                long fileId = long.Parse(match.Groups[1].Value);
                if (validIds.Contains(fileId)) continue;

                string key = $"{Path.GetFileName(texturePath)} fileID {fileId}";
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }

            if (counts.Count > 0)
                missingByFile[file] = counts.Select(kv => $"{kv.Key} × {kv.Value}").ToList();
        }

        if (missingByFile.Count == 0)
        {
            message = $"[SpriteRef] Missing Sprite 없음. PNG 참조 {checkedReferences}개 검사.";
            Debug.Log(message);
            return 0;
        }

        var report = new StringBuilder($"[SpriteRef] Missing Sprite 참조가 있는 파일 {missingByFile.Count}개 (PNG 참조 {checkedReferences}개 검사):");
        foreach (var pair in missingByFile)
        {
            report.Append($"\n  {pair.Key}{(IsProtected(pair.Key) ? " [보호 파일]" : "")}");
            foreach (string line in pair.Value) report.Append($"\n    - {line}");
        }
        message = report.ToString();
        Debug.LogError(message);
        return missingByFile.Count;
    }

    private static string ToAssetPath(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return fullPath.Substring(projectRoot.Length + 1).Replace('\\', '/');
    }
}
