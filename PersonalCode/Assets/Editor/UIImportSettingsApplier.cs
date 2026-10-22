using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Docs/UI/import_settings*.csv를 읽어 PNG 가져오기 설정을 일괄 적용한다.
/// 같은 PNG가 여러 CSV에 있으면 PriorityOrder 순서로 앞선 CSV를 따른다.
/// Multiple(조각 1개) → Single로 바뀌는 PNG는 씬·프리팹의 스프라이트 참조 fileID를 21300000으로 옮긴다.
/// </summary>
public static class UIImportSettingsApplier
{
    private const string MenuPath = "Tools/Yutnori/Apply UI Import Settings";
    private const string DryRunMenuPath = "Tools/Yutnori/Apply UI Import Settings (Dry Run)";
    private const string CsvFolder = "Docs/UI";
    private const string CsvPattern = "import_settings*.csv";

    // 앞에 있을수록 우선한다. 목록에 없는 CSV는 이 뒤에 파일 이름 순으로 붙는다.
    private static readonly string[] PriorityOrder =
    {
        "import_settings_layout.csv",
        "import_settings_4p.csv",
        "import_settings.csv",
    };

    // 2조각 이상으로 잘려 있어도 Single로 바꾸기로 승인한 PNG. 참조가 하나라도 있으면 바꾸지 않는다.
    private static readonly string[] ApprovedSlicedConversions =
    {
        "Assets/Art/Board/Moon/brd_moon_corner.png",
        // screens_spec 0-2 및 screens CSV에서 Single 적용을 명시한 새 로고.
        "Assets/Art/UI/Outgame/Logo/logo_title.png",
    };

    // 참조 fileID를 고쳐도 되는 파일. 계획에 이 밖의 파일이 있으면 실행하지 않는다.
    private static readonly string[] RewriteAllowList =
    {
        "Assets/Scenes/Game.unity",
        "Assets/Prefabs/UI/GameHudPanel_4P.prefab",
        "Assets/Prefabs/Board/Node_Center.prefab",
        "Assets/Prefabs/Board/Node_Corner.prefab",
        "Assets/Prefabs/Board/Node_End.prefab",
        "Assets/Prefabs/Board/Node_Normal.prefab",
        "Assets/Prefabs/Board/Node_Start.prefab",
    };

    private static readonly string[] RequiredColumns =
    {
        "relative_path", "sprite_mode", "pixels_per_unit", "mesh_type", "filter_mode",
        "compression", "alpha_is_transparency", "pivot_x", "pivot_y",
        "border_left", "border_bottom", "border_right", "border_top",
    };

    private class Row
    {
        public string CsvName;
        public int LineNumber;
        public string Path;
        public SpriteImportMode SpriteMode;
        public float PixelsPerUnit;
        public SpriteMeshType MeshType;
        public FilterMode Filter;
        public TextureImporterCompression Compression;
        public bool AlphaIsTransparency;
        public Vector2 Pivot;
        public Vector4 Border; // left, bottom, right, top

        public string Source => $"{CsvName}:{LineNumber}";
    }

    /// <summary>Multiple(조각 1개)에서 Single로 바꾸는 PNG.</summary>
    private class Conversion
    {
        public Row Row;
        public string Guid;
        public string OldName;
        public long OldFileId;
        public List<long> SlicedIds; // 승인된 2조각 이상 PNG의 조각 fileID. 참조가 있으면 변환하지 않는다.
    }

    /// <summary>Multiple로 2조각 이상 잘려 있어 자동으로 바꾸지 않는 PNG.</summary>
    private class SlicedHold
    {
        public Row Row;
        public string Guid;
        public List<(string name, long fileId)> Slices;
    }

    private class Plan
    {
        public List<string> CsvFiles = new List<string>();
        public List<string> CsvPaths = new List<string>();
        public string CsvSummary;
        public int TotalRows;
        public List<string> Duplicates = new List<string>();
        public List<string> ParseErrors = new List<string>();
        public List<string> MissingFiles = new List<string>();
        public List<string> NotTexture = new List<string>();

        public List<Row> Direct = new List<Row>();
        public List<Conversion> Conversions = new List<Conversion>();
        public List<SlicedHold> SlicedHolds = new List<SlicedHold>();
        public List<string> ProtectedHolds = new List<string>();

        // 파일 → (guid, fileID) → 개수. 변환·보류 대상 PNG만 센다.
        public Dictionary<string, Dictionary<(string guid, long fileId), int>> References;

        public int ApplyCount => Direct.Count + Conversions.Count;

        public Dictionary<string, long> OldFileIdByGuid() =>
            Conversions.ToDictionary(c => c.Guid, c => c.OldFileId);

        public int CountChanges(string file)
        {
            if (!References.TryGetValue(file, out var counts)) return 0;
            return Conversions.Sum(c => counts.TryGetValue((c.Guid, c.OldFileId), out int n) ? n : 0);
        }

        public List<string> FilesToRewrite() =>
            References.Keys.Where(f => CountChanges(f) > 0).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    [MenuItem(DryRunMenuPath)]
    public static void DryRun()
    {
        Plan plan = BuildPlan();
        if (plan == null) return;
        var report = new ReportWriter();
        LogPlan(plan, dryRun: true, report);
        report.Save("dry-run");
    }

    [MenuItem(MenuPath)]
    public static void ApplyMenu() => Apply(showDialog: true);

    [MenuItem("Tools/Yutnori/Apply UI Import Settings From CSV")]
    public static void ApplySelectedCsvMenu()
    {
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CsvFolder);
        string selected = EditorUtility.OpenFilePanel("UI Import Settings CSV", folder, "csv");
        if (string.IsNullOrEmpty(selected)) return;
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(selected)), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Docs/UI 폴더의 CSV만 선택할 수 있습니다.");
        ApplyCsv(Path.GetFileName(selected), true);
    }

    public static void ApplyCsv(string csvName, bool showDialog = false) => Apply(showDialog, csvName);

    public static void Apply(bool showDialog, string csvName = null)
    {
        Plan plan = BuildPlan(csvName);
        if (plan == null) return;

        List<string> filesToRewrite = plan.FilesToRewrite();
        if (!CanRewrite(filesToRewrite)) return;

        int changeCount = filesToRewrite.Sum(plan.CountChanges);
        if (showDialog && !EditorUtility.DisplayDialog(
                "Apply UI Import Settings",
                $"PNG {plan.ApplyCount}개에 가져오기 설정을 적용합니다.\n" +
                $"그중 {plan.Conversions.Count}개는 Multiple → Single로 바뀌고,\n" +
                $"씬·프리팹 {filesToRewrite.Count}개에서 참조 {changeCount}곳의 fileID를 옮깁니다.\n\n" +
                "Git 커밋을 했는지 확인하세요.",
                "적용", "취소"))
        {
            return;
        }

        // 열린 씬이 수정 대상이면 잠시 닫았다가 디스크에서 다시 연다.
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        bool reopenScenes = sceneSetup.Any(s => filesToRewrite.Contains(s.path));
        if (reopenScenes) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int changedImporters = 0;
        var rewritten = new List<string>();
        try
        {
            changedImporters = ApplyImporters(plan);

            AssetDatabase.StartAssetEditing();
            try
            {
                Dictionary<string, long> oldIds = plan.OldFileIdByGuid();
                foreach (string file in filesToRewrite)
                {
                    int replaced = SpriteReferenceTool.RewriteReferences(file, oldIds);
                    rewritten.Add($"{file}: {replaced}곳");
                    AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
        finally
        {
            if (reopenScenes) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }

        var report = new ReportWriter();
        LogPlan(plan, dryRun: false, report);
        report.Log($"[UIImport] 완료: 적용 {plan.ApplyCount}개 (실제로 바뀐 파일 {changedImporters}개, 이미 맞던 파일 {plan.ApplyCount - changedImporters}개)" +
                  (rewritten.Count > 0 ? "\n참조를 옮긴 파일:\n  " + string.Join("\n  ", rewritten) : ""));

        SpriteReferenceTool.Verify(SpriteReferenceTool.FindYamlFiles(), out string verifyReport);
        report.Note(verifyReport);
        report.Save("apply");
    }

    private static Plan BuildPlan(string selectedCsv = null)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, CsvFolder);

        if (!Directory.Exists(folder))
        {
            Debug.LogError($"[UIImport] CSV 폴더가 없습니다: {CsvFolder}/");
            return null;
        }

        foreach (string name in PriorityOrder.Where(name => selectedCsv == null && !File.Exists(Path.Combine(folder, name))))
        {
            Debug.LogError($"[UIImport] CSV 파일이 없습니다: {CsvFolder}/{name}");
        }

        if (selectedCsv != null && (Path.GetFileName(selectedCsv) != selectedCsv ||
            !selectedCsv.StartsWith("import_settings", StringComparison.Ordinal) ||
            !selectedCsv.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(folder, selectedCsv))))
            throw new InvalidOperationException($"유효한 UI CSV가 아닙니다: {selectedCsv}");
        var plan = new Plan();
        plan.CsvFiles = (selectedCsv == null ? Directory.GetFiles(folder, CsvPattern) : new[] { Path.Combine(folder, selectedCsv) })
            .Select(Path.GetFileName)
            .OrderBy(GetPriority)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (plan.CsvFiles.Count == 0)
        {
            Debug.LogError($"[UIImport] {CsvFolder}/ 에 {CsvPattern} 파일이 없습니다.");
            return null;
        }

        // CSV 읽기. 우선순위가 높은 파일부터 읽으므로 먼저 들어온 행이 이긴다.
        var rowsByPath = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var csvSummary = new StringBuilder();
        foreach (string csvName in plan.CsvFiles)
        {
            List<Row> rows = ReadCsv(Path.Combine(folder, csvName), csvName, plan.ParseErrors);
            plan.TotalRows += rows.Count;
            csvSummary.Append($"\n  {csvName}: {rows.Count}행");

            foreach (Row row in rows)
            {
                if (rowsByPath.TryGetValue(row.Path, out Row kept))
                {
                    plan.Duplicates.Add($"{row.Path} — {kept.Source} 적용, {row.Source} 무시");
                    continue;
                }
                rowsByPath.Add(row.Path, row);
            }
        }
        plan.CsvSummary = csvSummary.ToString();
        plan.CsvPaths = rowsByPath.Keys.ToList();

        // PNG별 분류
        foreach (Row row in rowsByPath.Values)
        {
            if (!File.Exists(Path.Combine(projectRoot, row.Path)))
            {
                plan.MissingFiles.Add($"{row.Path} ({row.Source})");
                continue;
            }
            if (!(AssetImporter.GetAtPath(row.Path) is TextureImporter importer))
            {
                plan.NotTexture.Add($"{row.Path} ({row.Source})");
                continue;
            }

            bool needsConversion = importer.textureType == TextureImporterType.Sprite &&
                                   importer.spriteImportMode == SpriteImportMode.Multiple &&
                                   row.SpriteMode == SpriteImportMode.Single;
            if (!needsConversion)
            {
                plan.Direct.Add(row);
                continue;
            }

            string guid = AssetDatabase.AssetPathToGUID(row.Path);
            List<(string name, long fileId)> slices = SpriteReferenceTool.GetSpriteIds(row.Path);
            bool approved = ApprovedSlicedConversions.Contains(row.Path, StringComparer.OrdinalIgnoreCase);
            if (slices.Count >= 2 && !approved)
            {
                plan.SlicedHolds.Add(new SlicedHold { Row = row, Guid = guid, Slices = slices });
            }
            else
            {
                plan.Conversions.Add(new Conversion
                {
                    Row = row,
                    Guid = guid,
                    OldName = slices.Count == 1 ? slices[0].name
                        : slices.Count == 0 ? "(조각 없음)" : $"(승인된 {slices.Count}조각)",
                    OldFileId = slices.Count == 1 ? slices[0].fileId : 0,
                    SlicedIds = slices.Count >= 2 ? slices.Select(x => x.fileId).ToList() : null,
                });
            }
        }

        // 변환·보류 PNG를 가리키는 참조 수집
        var guids = new HashSet<string>(plan.Conversions.Select(c => c.Guid).Concat(plan.SlicedHolds.Select(h => h.Guid)));
        plan.References = SpriteReferenceTool.CountReferences(SpriteReferenceTool.FindYamlFiles(), guids);

        // 승인된 2조각 이상 PNG라도 조각을 참조하는 곳이 생겼으면 보류한다.
        foreach (Conversion conversion in plan.Conversions.Where(c => c.SlicedIds != null).ToList())
        {
            bool referenced = plan.References.Values.Any(counts =>
                counts.Keys.Any(k => k.guid == conversion.Guid && conversion.SlicedIds.Contains(k.fileId)));
            if (!referenced) continue;

            plan.Conversions.Remove(conversion);
            plan.SlicedHolds.Add(new SlicedHold
            {
                Row = conversion.Row,
                Guid = conversion.Guid,
                Slices = SpriteReferenceTool.GetSpriteIds(conversion.Row.Path),
            });
        }

        // 보호 파일이 가리키는 PNG는 변환하지 않는다 (보호 파일을 고치지 않으면 끊기므로).
        foreach (Conversion conversion in plan.Conversions.ToList())
        {
            var protectedFiles = plan.References
                .Where(pair => SpriteReferenceTool.IsProtected(pair.Key) &&
                               pair.Value.Keys.Any(k => k.guid == conversion.Guid && k.fileId == conversion.OldFileId))
                .Select(pair => pair.Key)
                .ToList();
            if (protectedFiles.Count == 0) continue;

            plan.Conversions.Remove(conversion);
            plan.ProtectedHolds.Add($"{conversion.Row.Path} — 참조하는 보호 파일: {string.Join(", ", protectedFiles)}");
        }

        return plan;
    }

    private static bool CanRewrite(List<string> filesToRewrite)
    {
        var outside = filesToRewrite.Where(f => !RewriteAllowList.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
        if (outside.Count > 0)
        {
            Debug.LogError("[UIImport] 허용 목록 밖의 파일을 고쳐야 해서 중단합니다:\n  " + string.Join("\n  ", outside));
            return false;
        }

        var dirty = new List<string>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.isDirty && filesToRewrite.Contains(scene.path)) dirty.Add(scene.path);
        }
        if (dirty.Count > 0)
        {
            Debug.LogError("[UIImport] 저장하지 않은 변경이 있는 씬이 수정 대상입니다. 저장한 뒤 다시 실행하세요:\n  " + string.Join("\n  ", dirty));
            return false;
        }

        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && filesToRewrite.Contains(stage.assetPath))
        {
            Debug.LogError($"[UIImport] 프리팹 편집 모드에서 수정 대상 프리팹을 열고 있습니다. 닫은 뒤 다시 실행하세요: {stage.assetPath}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 가져오기 설정을 적용하고 실제로 바뀐 PNG 수를 돌려준다.
    /// </summary>
    private static int ApplyImporters(Plan plan)
    {
        var rows = plan.Direct.Concat(plan.Conversions.Select(c => c.Row)).ToList();
        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                EditorUtility.DisplayProgressBar("Apply UI Import Settings", row.Path, (float)(i + 1) / rows.Count);

                var importer = (TextureImporter)AssetImporter.GetAtPath(row.Path);
                if (!ApplyRow(importer, row)) continue;

                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
        return changed;
    }

    private static void LogPlan(Plan plan, bool dryRun, ReportWriter report)
    {
        string tag = dryRun ? "[UIImport][Dry Run]" : "[UIImport]";
        List<string> filesToRewrite = plan.FilesToRewrite();

        // 1. 요약
        var summary = new StringBuilder();
        summary.Append($"{tag} CSV {plan.CsvFiles.Count}개, 행 합계 {plan.TotalRows}{plan.CsvSummary}");
        summary.Append($"\n적용 대상 {plan.ApplyCount}개 = 그대로 적용 {plan.Direct.Count} + Multiple → Single 변환 {plan.Conversions.Count}");
        summary.Append($"\n참조를 옮길 파일 {filesToRewrite.Count}개, 참조 {filesToRewrite.Sum(plan.CountChanges)}곳");
        report.Log(summary.ToString());

        foreach (string error in plan.ParseErrors) report.LogError($"{tag} 읽기 오류: {error}");
        foreach (string dup in plan.Duplicates) report.LogWarning($"{tag} 중복 항목: {dup}");
        if (plan.MissingFiles.Count > 0)
            report.LogError($"{tag} CSV에 있지만 파일이 없는 항목 {plan.MissingFiles.Count}개:\n  " + string.Join("\n  ", plan.MissingFiles));
        if (plan.NotTexture.Count > 0)
            report.LogError($"{tag} 텍스처로 가져오지 못한 항목 {plan.NotTexture.Count}개:\n  " + string.Join("\n  ", plan.NotTexture));

        // 2. PNG별 fileID 변경
        if (plan.Conversions.Count > 0)
        {
            var conversions = new StringBuilder($"{tag} Multiple → Single 변환 {plan.Conversions.Count}개 (예전 fileID → {SpriteReferenceTool.SingleSpriteFileId}):");
            foreach (Conversion c in plan.Conversions.OrderBy(c => c.Row.Path, StringComparer.Ordinal))
            {
                var referencingFiles = plan.References
                    .Select(pair => (file: pair.Key, count: pair.Value.TryGetValue((c.Guid, c.OldFileId), out int n) ? n : 0))
                    .Where(x => x.count > 0)
                    .ToList();
                int total = referencingFiles.Sum(x => x.count);
                conversions.Append($"\n  {c.Row.Path}  [{c.OldName}] {c.OldFileId} → {SpriteReferenceTool.SingleSpriteFileId}  참조 {total}곳");
                foreach (var x in referencingFiles) conversions.Append($"\n      {x.file}: {x.count}");
            }
            report.Log(conversions.ToString());
        }

        // 3. 파일별 바뀌는 연결 수
        if (filesToRewrite.Count > 0)
        {
            var files = new StringBuilder($"{tag} 파일별 바뀌는 연결 수 ({filesToRewrite.Count}개 파일):");
            foreach (string file in filesToRewrite) files.Append($"\n  {file}: {plan.CountChanges(file)}");
            report.Log(files.ToString());
        }

        // 4. 변환 대상 PNG를 이미 잘못된 fileID로 가리키는 참조 (변환과 무관하게 끊겨 있음)
        var alreadyBroken = new List<string>();
        foreach (Conversion c in plan.Conversions)
        {
            foreach (var pair in plan.References)
            {
                foreach (var key in pair.Value.Keys.Where(k => k.guid == c.Guid && k.fileId != c.OldFileId && k.fileId != 2800000))
                    alreadyBroken.Add($"{pair.Key}: {Path.GetFileName(c.Row.Path)} fileID {key.fileId} × {pair.Value[key]}");
            }
        }
        if (alreadyBroken.Count > 0)
            report.LogWarning($"{tag} 변환 전부터 맞지 않는 fileID를 가리키는 참조 (자동으로 옮기지 않음):\n  " + string.Join("\n  ", alreadyBroken));

        // 5. 2조각 이상으로 잘린 PNG
        if (plan.SlicedHolds.Count > 0)
        {
            var sliced = new StringBuilder($"{tag} 2조각 이상으로 잘려 있어 바꾸지 않은 PNG {plan.SlicedHolds.Count}개:");
            foreach (SlicedHold hold in plan.SlicedHolds)
            {
                sliced.Append($"\n  {hold.Row.Path} — 조각 {hold.Slices.Count}개");
                foreach (var slice in hold.Slices) sliced.Append($"\n      {slice.name} = {slice.fileId}");

                var refs = plan.References
                    .SelectMany(pair => pair.Value.Where(kv => kv.Key.guid == hold.Guid)
                        .Select(kv => (file: pair.Key, fileId: kv.Key.fileId, count: kv.Value)))
                    .ToList();
                if (refs.Count == 0)
                {
                    sliced.Append("\n    참조: 없음");
                    continue;
                }
                foreach (var r in refs)
                {
                    string sliceName = r.fileId == 2800000 ? "(텍스처)"
                        : hold.Slices.Where(s => s.fileId == r.fileId).Select(s => s.name).DefaultIfEmpty("(없는 조각)").First();
                    sliced.Append($"\n    참조: {r.file} → {sliceName} × {r.count}");
                }
            }
            report.LogWarning(sliced.ToString());
        }

        if (plan.ProtectedHolds.Count > 0)
            report.LogWarning($"{tag} 보호 파일이 참조해 변환하지 않은 PNG {plan.ProtectedHolds.Count}개:\n  " + string.Join("\n  ", plan.ProtectedHolds));

        // 6. 행 합계와 적용 수 차이
        if (plan.ApplyCount != plan.TotalRows)
        {
            var diff = new StringBuilder();
            diff.Append($"{tag} CSV 행 합계({plan.TotalRows})와 적용 수({plan.ApplyCount})가 다릅니다. 차이 {plan.TotalRows - plan.ApplyCount}개:");
            diff.Append($"\n  중복으로 무시: {plan.Duplicates.Count}");
            diff.Append($"\n  파일 없음: {plan.MissingFiles.Count}");
            diff.Append($"\n  텍스처 아님: {plan.NotTexture.Count}");
            diff.Append($"\n  2조각 이상 보류: {plan.SlicedHolds.Count}");
            diff.Append($"\n  보호 파일 참조로 보류: {plan.ProtectedHolds.Count}");
            foreach (string item in plan.Duplicates) diff.Append($"\n  - 중복: {item}");
            foreach (string item in plan.MissingFiles) diff.Append($"\n  - 파일 없음: {item}");
            foreach (string item in plan.NotTexture) diff.Append($"\n  - 텍스처 아님: {item}");
            foreach (SlicedHold item in plan.SlicedHolds) diff.Append($"\n  - 2조각 이상: {item.Row.Path}");
            foreach (string item in plan.ProtectedHolds) diff.Append($"\n  - 보호 파일: {item}");
            report.LogWarning(diff.ToString());
        }

        LogPngsNotInCsv(plan.CsvPaths, report);
    }

    /// <summary>
    /// 콘솔에 출력하면서 같은 내용을 Logs/ui_import_report.txt에도 모은다.
    /// </summary>
    private class ReportWriter
    {
        public const string ReportPath = "Logs/ui_import_report.txt";
        private readonly StringBuilder text = new StringBuilder();

        public void Log(string message) { Debug.Log(message); Append("INFO", message); }
        public void LogWarning(string message) { Debug.LogWarning(message); Append("WARN", message); }
        public void LogError(string message) { Debug.LogError(message); Append("ERROR", message); }

        /// <summary>콘솔에는 이미 나간 내용을 보고서에만 붙인다.</summary>
        public void Note(string message) => Append("NOTE", message);

        private void Append(string level, string message) => text.Append($"[{level}] {message}\n\n");

        public void Save(string mode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, $"# UI Import Settings ({mode}) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{text}", new UTF8Encoding(false));
            Debug.Log($"[UIImport] 보고서 저장: {ReportPath}");
        }
    }

    private static int GetPriority(string csvName)
    {
        int index = Array.IndexOf(PriorityOrder, csvName);
        return index >= 0 ? index : PriorityOrder.Length;
    }

    /// <summary>
    /// 값이 다를 때만 바꾸고, 하나라도 바뀌었으면 true를 돌려준다.
    /// </summary>
    private static bool ApplyRow(TextureImporter importer, Row row)
    {
        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != row.SpriteMode)
        {
            importer.spriteImportMode = row.SpriteMode;
            changed = true;
        }
        if (importer.textureCompression != row.Compression)
        {
            importer.textureCompression = row.Compression;
            changed = true;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (!Mathf.Approximately(settings.spritePixelsPerUnit, row.PixelsPerUnit))
        {
            settings.spritePixelsPerUnit = row.PixelsPerUnit;
            changed = true;
        }
        if (settings.spriteMeshType != row.MeshType)
        {
            settings.spriteMeshType = row.MeshType;
            changed = true;
        }
        if (settings.filterMode != row.Filter)
        {
            settings.filterMode = row.Filter;
            changed = true;
        }
        if (settings.alphaIsTransparency != row.AlphaIsTransparency)
        {
            settings.alphaIsTransparency = row.AlphaIsTransparency;
            changed = true;
        }

        int alignment = (int)ToAlignment(row.Pivot);
        if (settings.spriteAlignment != alignment || settings.spritePivot != row.Pivot)
        {
            settings.spriteAlignment = alignment;
            settings.spritePivot = row.Pivot;
            changed = true;
        }
        if (settings.spriteBorder != row.Border)
        {
            settings.spriteBorder = row.Border;
            changed = true;
        }

        if (changed) importer.SetTextureSettings(settings);
        return changed;
    }

    private static SpriteAlignment ToAlignment(Vector2 pivot)
    {
        if (pivot == new Vector2(0.5f, 0.5f)) return SpriteAlignment.Center;
        if (pivot == new Vector2(0f, 1f)) return SpriteAlignment.TopLeft;
        if (pivot == new Vector2(0.5f, 1f)) return SpriteAlignment.TopCenter;
        if (pivot == new Vector2(1f, 1f)) return SpriteAlignment.TopRight;
        if (pivot == new Vector2(0f, 0.5f)) return SpriteAlignment.LeftCenter;
        if (pivot == new Vector2(1f, 0.5f)) return SpriteAlignment.RightCenter;
        if (pivot == new Vector2(0f, 0f)) return SpriteAlignment.BottomLeft;
        if (pivot == new Vector2(0.5f, 0f)) return SpriteAlignment.BottomCenter;
        if (pivot == new Vector2(1f, 0f)) return SpriteAlignment.BottomRight;
        return SpriteAlignment.Custom;
    }

    private static List<Row> ReadCsv(string fullPath, string csvName, List<string> errors)
    {
        var rows = new List<Row>();
        string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            errors.Add($"{csvName}: 빈 파일");
            return rows;
        }

        string[] header = lines[0].Trim().TrimStart('﻿').Split(',').Select(h => h.Trim()).ToArray();
        var column = new Dictionary<string, int>();
        for (int i = 0; i < header.Length; i++) column[header[i]] = i;

        var missingColumns = RequiredColumns.Where(c => !column.ContainsKey(c)).ToList();
        if (missingColumns.Count > 0)
        {
            errors.Add($"{csvName}: 헤더에 열이 없습니다 — {string.Join(", ", missingColumns)}");
            return rows;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            int lineNumber = i + 1;
            string[] cells = line.Split(',').Select(c => c.Trim()).ToArray();
            if (cells.Length < header.Length)
            {
                errors.Add($"{csvName}:{lineNumber}: 열 수 부족 ({cells.Length}/{header.Length})");
                continue;
            }

            string Cell(string name) => cells[column[name]];

            try
            {
                rows.Add(new Row
                {
                    CsvName = csvName,
                    LineNumber = lineNumber,
                    Path = Cell("relative_path").Replace('\\', '/'),
                    SpriteMode = ParseSpriteMode(Cell("sprite_mode")),
                    PixelsPerUnit = ParsePositiveFloat(Cell("pixels_per_unit")),
                    MeshType = ParseMeshType(Cell("mesh_type")),
                    Filter = ParseFilter(Cell("filter_mode")),
                    Compression = ParseCompression(Cell("compression")),
                    AlphaIsTransparency = ParseBool(Cell("alpha_is_transparency")),
                    Pivot = new Vector2(ParseFloat(Cell("pivot_x")), ParseFloat(Cell("pivot_y"))),
                    Border = new Vector4(
                        ParseFloat(Cell("border_left")),
                        ParseFloat(Cell("border_bottom")),
                        ParseFloat(Cell("border_right")),
                        ParseFloat(Cell("border_top"))),
                });
            }
            catch (FormatException e)
            {
                errors.Add($"{csvName}:{lineNumber}: {e.Message}");
            }
        }

        return rows;
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)) return result;
        throw new FormatException($"숫자가 아닙니다: '{value}'");
    }

    private static float ParsePositiveFloat(string value)
    {
        float result = ParseFloat(value);
        if (result <= 0f) throw new FormatException($"0보다 커야 합니다: '{value}'");
        return result;
    }

    private static bool ParseBool(string value)
    {
        if (bool.TryParse(value, out bool result)) return result;
        throw new FormatException($"true/false가 아닙니다: '{value}'");
    }

    private static SpriteImportMode ParseSpriteMode(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "single": return SpriteImportMode.Single;
            case "multiple": return SpriteImportMode.Multiple;
            default: throw new FormatException($"알 수 없는 sprite_mode: '{value}'");
        }
    }

    private static SpriteMeshType ParseMeshType(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "fullrect": return SpriteMeshType.FullRect;
            case "tight": return SpriteMeshType.Tight;
            default: throw new FormatException($"알 수 없는 mesh_type: '{value}'");
        }
    }

    private static FilterMode ParseFilter(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "point": return FilterMode.Point;
            case "bilinear": return FilterMode.Bilinear;
            case "trilinear": return FilterMode.Trilinear;
            default: throw new FormatException($"알 수 없는 filter_mode: '{value}'");
        }
    }

    private static TextureImporterCompression ParseCompression(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "none": return TextureImporterCompression.Uncompressed;
            case "lowquality": return TextureImporterCompression.CompressedLQ;
            case "normalquality": return TextureImporterCompression.Compressed;
            case "highquality": return TextureImporterCompression.CompressedHQ;
            default: throw new FormatException($"알 수 없는 compression: '{value}'");
        }
    }

    /// <summary>
    /// Assets/Art 아래 PNG 중 어느 CSV에도 없는 파일을 알려 준다. 설정은 바꾸지 않는다.
    /// </summary>
    private static void LogPngsNotInCsv(IEnumerable<string> csvPaths, ReportWriter report)
    {
        var known = new HashSet<string>(csvPaths, StringComparer.OrdinalIgnoreCase);
        var extra = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !known.Contains(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (extra.Count > 0)
        {
            report.LogWarning($"[UIImport] Assets/Art 아래 PNG 중 CSV에 없는 파일 {extra.Count}개 (설정 안 함):\n  " + string.Join("\n  ", extra));
        }
    }
}
