using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class YutNoriIntegrationTool
{
    private const string GamePath = "Assets/Scenes/Game.unity";
    private const string BackupPath = "Assets/Scenes/Game_BeforeTeamIntegration.unity";
    private const string SourcePath = "Assets/Scenes/PJWScene.unity";

    [MenuItem("Tools/YutNori/Integrate Game Scene")]
    public static void IntegrateGameScene()
    {
        EditorSceneManager.SaveOpenScenes();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            AssetDatabase.CopyAsset(GamePath, BackupPath);
            AssetDatabase.SaveAssets();
        }

        Scene sourceScene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
        BoardView sourceBoard = Object.FindFirstObjectByType<BoardView>();
        PathView sourcePath = Object.FindFirstObjectByType<PathView>();
        PieceView[] sourcePieces = Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None);
        if (sourceBoard == null || sourcePath == null || sourcePieces.Length < 4)
            throw new System.InvalidOperationException("PJWScene에서 BoardView, PathView 또는 말을 찾지 못했습니다.");

        var sourceBundle = new GameObject("__IntegrationBundle");
        sourceBoard.transform.SetParent(sourceBundle.transform, true);
        foreach (NodeView node in sourceBoard.GetNodeViews())
            if (node.transform.parent == null) node.transform.SetParent(sourceBundle.transform, true);

        Transform sourcePieceRoot = sourcePieces[0].transform.parent;
        if (sourcePieceRoot != null) sourcePieceRoot.SetParent(sourceBundle.transform, true);
        Transform sourcePathRoot = sourcePath.transform.root;
        if (sourcePathRoot != sourceBundle.transform) sourcePathRoot.SetParent(sourceBundle.transform, true);

        GameObject gameplay = Object.Instantiate(sourceBundle);
        gameplay.name = "Gameplay";

        Scene gameScene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
        SceneManager.MoveGameObjectToScene(gameplay, gameScene);
        SceneManager.SetActiveScene(gameScene);

        GameObject artwork = gameScene.GetRootGameObjects().FirstOrDefault(go => go.name == "Board");
        if (artwork == null) throw new System.InvalidOperationException("Game 씬의 Board 오브젝트를 찾지 못했습니다.");
        artwork.name = "BoardArtwork";

        Transform guideRoot = artwork.transform.Find("NodePositions");
        if (guideRoot == null || guideRoot.childCount != 29)
            throw new System.InvalidOperationException("Game 씬의 NodePositions에는 29개 위치가 필요합니다.");

        Vector3[] guidePositions = guideRoot.Cast<Transform>()
            .OrderBy(t => t.name)
            .Select(t => t.position)
            .ToArray();

        Component legacyBoard = artwork.GetComponent("BoardView_");
        if (legacyBoard != null) Object.DestroyImmediate(legacyBoard);

        GameObject oldPieceRoot = gameScene.GetRootGameObjects().FirstOrDefault(go => go.name == "Player Group");
        Vector3[] homePositions = oldPieceRoot != null
            ? oldPieceRoot.transform.Cast<Transform>().OrderBy(t => t.name).Select(t => t.position).ToArray()
            : new[] { new Vector3(-4.75f, -7.15f), new Vector3(-3.03f, -7.15f), new Vector3(-1.47f, -7.24f), new Vector3(0.13f, -7.24f) };
        if (oldPieceRoot != null) Object.DestroyImmediate(oldPieceRoot);

        BoardView board = gameplay.GetComponentInChildren<BoardView>(true);
        PathView path = gameplay.GetComponentInChildren<PathView>(true);
        NodeView[] nodes = gameplay.GetComponentsInChildren<NodeView>(true);
        PositionNodes(nodes, guidePositions);

        PieceView[] allPieces = gameplay.GetComponentsInChildren<PieceView>(true);
        List<PieceView> keptPieces = allPieces
            .GroupBy(piece => piece.Team)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.OrderBy(piece => piece.name).Take(2))
            .ToList();
        foreach (PieceView piece in allPieces)
            if (!keptPieces.Contains(piece)) Object.DestroyImmediate(piece.gameObject);

        for (int i = 0; i < keptPieces.Count; i++)
        {
            keptPieces[i].name = $"Team{keptPieces[i].Team + 1}_Piece{i % 2 + 1}";
            keptPieces[i].transform.position = homePositions[Mathf.Min(i, homePositions.Length - 1)];
        }

        GameFlowController controller = gameScene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<GameFlowController>(true))
            .FirstOrDefault();
        if (controller == null) throw new System.InvalidOperationException("Game 씬의 GameFlowController를 찾지 못했습니다.");

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("boardView").objectReferenceValue = board;
        serialized.FindProperty("pathView").objectReferenceValue = path;
        SerializedProperty piecesProperty = serialized.FindProperty("pieceViews");
        piecesProperty.arraySize = keptPieces.Count;
        for (int i = 0; i < keptPieces.Count; i++)
            piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = keptPieces[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene, GamePath);
        EditorSceneManager.CloseScene(sourceScene, true);
        Selection.activeGameObject = controller.gameObject;
        Debug.Log("[YutIntegration] Game 씬 통합 완료: BoardGraph 30노드, 팀별 말 2개, PathView, GameFlowController 연결");
    }

    [MenuItem("Tools/YutNori/Add Turn And Result UI")]
    public static void AddTurnAndResultUI()
    {
        Scene gameScene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameFlowController controller = Object.FindFirstObjectByType<GameFlowController>();
        if (canvas == null || controller == null)
            throw new System.InvalidOperationException("Game 씬에서 Canvas 또는 GameFlowController를 찾지 못했습니다.");

        Transform existing = canvas.transform.Find("GameStatusPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var panelObject = new GameObject("GameStatusPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -24f);
        panel.sizeDelta = new Vector2(620f, 145f);
        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.06f, 0.12f, 0.22f, 0.88f);
        background.raycastTarget = false;

        TMP_Text turn = CreateText(panel, "TurnText", new Vector2(0f, -35f), new Vector2(590f, 45f), 28f);
        turn.text = "Team 1 Turn  |  Throw Yut";
        turn.color = new Color(0.55f, 0.95f, 1f);

        TMP_Text result = CreateText(panel, "YutResultText", new Vector2(0f, -91f), new Vector2(590f, 65f), 38f);
        result.text = "Result: -";
        result.color = new Color(1f, 0.88f, 0.35f);
        result.fontStyle = FontStyles.Bold;

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("turnText").objectReferenceValue = turn;
        serialized.FindProperty("yutResultText").objectReferenceValue = result;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 컨트롤러에 연결되지 않은 PJWScene 테스트용 말은 Game 씬에서 제거한다.
        var connectedPieces = new HashSet<PieceView>();
        SerializedProperty connectedProperty = serialized.FindProperty("pieceViews");
        for (int i = 0; i < connectedProperty.arraySize; i++)
        {
            PieceView connected = connectedProperty.GetArrayElementAtIndex(i).objectReferenceValue as PieceView;
            if (connected != null) connectedPieces.Add(connected);
        }
        foreach (PieceView piece in Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None))
        {
            if (!connectedPieces.Contains(piece)) Object.DestroyImmediate(piece.gameObject);
        }

        foreach (NodeView node in Object.FindObjectsByType<NodeView>(FindObjectsSortMode.None))
        {
            var nodeSerialized = new SerializedObject(node);
            nodeSerialized.FindProperty("outlineScale").floatValue = 1.55f;
            nodeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene);
        Selection.activeGameObject = panelObject;
        Debug.Log("[YutIntegration] 턴/윷 결과 UI와 말·노드 강조 표시를 연결했습니다.");
    }

    private static TMP_Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static void PositionNodes(IEnumerable<NodeView> nodes, IReadOnlyList<Vector3> positions)
    {
        foreach (NodeView node in nodes)
        {
            SpriteRenderer sprite = node.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null)
            {
                sprite.transform.localPosition = Vector3.zero;
                sprite.enabled = false;
            }

            if (node.Id == 0)
            {
                node.transform.position = new Vector3(positions[0].x, positions[0].y - 1.5f, 0f);
            }
            else if (node.Id == 20)
            {
                node.transform.position = positions[0];
            }
            else if (node.Id >= 1 && node.Id <= 19)
            {
                node.transform.position = positions[node.Id];
            }
            else
            {
                node.transform.position = DiagonalPosition(node.transform.position, positions);
            }
            node.name = $"Node_{node.Id:00}";
        }
    }

    private static Vector3 DiagonalPosition(Vector3 source, IReadOnlyList<Vector3> positions)
    {
        if (source.x > 0f && source.y > 0f) return source.y > 2f ? positions[20] : positions[21];
        if (source.x < -2.5f && source.y > 0f) return positions[25];
        if (source.x < -1f && source.y > 0f) return positions[26];
        if (source.x < -2.5f && source.y < 0f) return positions[24];
        if (source.x < -1f && source.y < 0f) return positions[23];
        if (source.x > 0f && source.y < 0f) return source.y < -1.7f ? positions[28] : positions[27];
        return positions[22];
    }
}
