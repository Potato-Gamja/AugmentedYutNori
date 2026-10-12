using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 판 레이아웃(BoardLayoutSO)을 서버용 json(resources/boards/{id}.json)으로 내보낸다.
// 서버는 이 json으로 이동을 직접 계산하므로, Unity에서 판을 고치면 반드시 다시 내보내서 서버 저장소에 올려야 한다.
//   Tools/Yutnori/Board Layout/서버 json과 비교   : 파일은 건드리지 않고 다른 판만 알려 준다
//   Tools/Yutnori/Board Layout/서버 json 내보내기 : 모든 판을 폴더에 쓴다 (바뀐 파일만)
//   (Project 창) BoardLayoutSO 우클릭 → 서버 json 복사 : 한 판만 클립보드로
// 내보낼 폴더는 처음 한 번 고르면 이 PC에 기억된다 (보통 서버 저장소의 src/main/resources/boards)
public static class BoardLayoutExporter
{
    private const string MenuRoot = "Tools/Yutnori/Board Layout/";
    private const string FolderPrefKey = "Yutnori.BoardLayoutExporter.Folder";
    private const string DefaultServerBoardsPath = "../../Spring/AugmentedYutNori-Server/src/main/resources/boards";

    [MenuItem(MenuRoot + "서버 json과 비교")]
    private static void CompareWithServer()
    {
        string folder = GetFolder(askIfMissing: true);
        if (folder == null) return;

        Run(folder, write: false);
    }

    [MenuItem(MenuRoot + "서버 json 내보내기")]
    private static void ExportToServer()
    {
        string folder = GetFolder(askIfMissing: true);
        if (folder == null) return;

        Run(folder, write: true);
    }

    [MenuItem(MenuRoot + "내보낼 폴더 다시 고르기")]
    private static void ChangeFolder()
    {
        EditorPrefs.DeleteKey(FolderPrefKey);
        string folder = GetFolder(askIfMissing: true);
        if (folder != null) Debug.Log($"[BoardLayoutExporter] 내보낼 폴더: {folder}");
    }

    [MenuItem("Assets/Yutnori/서버 json 복사")]
    private static void CopySelected()
    {
        var layout = Selection.activeObject as BoardLayoutSO;
        if (!TryValidate(layout, out string error))
        {
            Debug.LogError($"[BoardLayoutExporter] {layout.name}: {error}", layout);
            return;
        }

        EditorGUIUtility.systemCopyBuffer = layout.ToServerJson();
        Debug.Log($"[BoardLayoutExporter] {layout.name} → {layout.ServerBoardId}.json 내용을 클립보드에 복사했습니다.", layout);
    }

    [MenuItem("Assets/Yutnori/서버 json 복사", true)]
    private static bool CanCopySelected() => Selection.activeObject is BoardLayoutSO;

    // 비교 또는 내보내기. 결과는 콘솔에 한 번에 정리해서 보여 준다
    public static Result Run(string folder, bool write)
    {
        var result = new Result();
        var usedIds = new Dictionary<string, string>();

        foreach (BoardLayoutSO layout in FindLayouts())
        {
            string id = layout.ServerBoardId;
            if (usedIds.TryGetValue(id, out string other))
            {
                result.Errors.Add($"{layout.name}: 서버 id '{id}'가 {other}와 겹칩니다. 에셋 이름을 바꿔 주세요.");
                continue;
            }
            usedIds[id] = layout.name;

            if (!TryValidate(layout, out string error))
            {
                result.Errors.Add($"{layout.name}: {error}");
                continue;
            }

            string path = Path.Combine(folder, id + ".json");
            string json = layout.ToServerJson();
            // Windows에서 git이 줄바꿈을 CRLF로 바꿔 받는 경우가 있어 줄바꿈 차이는 같은 것으로 본다
            string existing = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;

            if (existing == json)
            {
                result.Same.Add(id);
                continue;
            }

            (existing == null ? result.Added : result.Changed).Add(id);
            if (write) File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        // 서버에는 있는데 Unity에 없는 판 (지우지는 않고 알려만 준다)
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                if (!usedIds.ContainsKey(id)) result.OnlyOnServer.Add(id);
            }
        }

        Log(folder, write, result);
        return result;
    }

    private static List<BoardLayoutSO> FindLayouts()
    {
        var layouts = new List<BoardLayoutSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:BoardLayoutSO"))
        {
            var layout = AssetDatabase.LoadAssetAtPath<BoardLayoutSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (layout != null) layouts.Add(layout);
        }
        layouts.Sort((a, b) => string.CompareOrdinal(a.ServerBoardId, b.ServerBoardId));
        return layouts;
    }

    private static bool TryValidate(BoardLayoutSO layout, out string error)
    {
        if (layout == null || layout.Definition == null)
        {
            error = "판 정의가 없습니다.";
            return false;
        }
        return layout.Definition.Validate(out error);
    }

    private static string GetFolder(bool askIfMissing)
    {
        string saved = EditorPrefs.GetString(FolderPrefKey, "");
        if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved)) return saved;
        if (!askIfMissing) return null;

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string suggestion = Path.GetFullPath(Path.Combine(projectRoot, DefaultServerBoardsPath));
        string start = Directory.Exists(suggestion) ? suggestion : projectRoot;

        string chosen = EditorUtility.OpenFolderPanel("서버 판 json 폴더 (src/main/resources/boards)", start, "");
        if (string.IsNullOrEmpty(chosen)) return null;

        EditorPrefs.SetString(FolderPrefKey, chosen);
        return chosen;
    }

    private static void Log(string folder, bool write, Result result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[BoardLayoutExporter] {(write ? "내보내기" : "비교")} 결과 ({folder})");
        if (result.Same.Count > 0) builder.AppendLine($"  같음: {string.Join(", ", result.Same)}");
        if (result.Changed.Count > 0) builder.AppendLine($"  {(write ? "덮어씀" : "다름")}: {string.Join(", ", result.Changed)}");
        if (result.Added.Count > 0) builder.AppendLine($"  {(write ? "새로 만듦" : "서버에 없음")}: {string.Join(", ", result.Added)}");
        if (result.OnlyOnServer.Count > 0) builder.AppendLine($"  Unity에 없는 서버 판: {string.Join(", ", result.OnlyOnServer)}");
        foreach (string error in result.Errors) builder.AppendLine($"  오류: {error}");

        if (write && (result.Changed.Count > 0 || result.Added.Count > 0))
            builder.AppendLine("  → 서버 저장소에서 바뀐 json을 커밋해 주세요.");
        else if (!write && (result.Changed.Count > 0 || result.Added.Count > 0))
            builder.AppendLine("  → 서버 판이 Unity와 다릅니다. '서버 json 내보내기'로 맞춰 주세요.");

        if (result.Errors.Count > 0) Debug.LogError(builder.ToString());
        else if (result.Changed.Count > 0 || result.Added.Count > 0) Debug.LogWarning(builder.ToString());
        else Debug.Log(builder.ToString());
    }

    public class Result
    {
        public readonly List<string> Same = new List<string>();
        public readonly List<string> Changed = new List<string>();
        public readonly List<string> Added = new List<string>();
        public readonly List<string> OnlyOnServer = new List<string>();
        public readonly List<string> Errors = new List<string>();
    }
}
