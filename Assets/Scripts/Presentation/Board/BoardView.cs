using System;
using System.Collections.Generic;
using UnityEngine;

// 판 화면. 씬의 NodeView(칸 오브젝트)와 BoardGraph(순수 데이터)를 이어 준다.
//  - Layout이 비어 있으면: 씬에 놓인 칸의 연결로 그래프를 만든다
//  - Layout이 있으면: 그 판 배치를 적용한다 (같은 번호·같은 종류의 칸 오브젝트는 다시 쓰고, 없는 칸은 프리팹으로 만든다)
//  - 실행 중 SwitchLayout: 판 모양만 제자리에서 바꾼다. 게임 상태는 그대로이고, 말은 같은 번호의 칸에 남는다
// 출발 칸은 종류가 Start인 칸(판에 하나)이고, 칸은 Node Root 아래에서 찾는다 (비워 두면 씬 전체)
public class BoardView : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("적용할 판. 비워 두면 씬에 놓인 칸을 그대로 사용")]
    [SerializeField] private BoardLayoutSO layout;
    [Tooltip("칸을 둘 부모. 위치는 이 부모 기준 로컬 좌표로 저장/복원된다. 비워 두면 칸은 씬 전체에서 찾고, 새 칸은 이 오브젝트 아래에 만든다")]
    [SerializeField] private Transform nodeRoot;
    [Tooltip("칸 종류별 프리팹. 판에 쓰이는 종류는 모두 있어야 한다")]
    [SerializeField] private List<NodePrefabEntry> nodePrefabs = new List<NodePrefabEntry>();

    [Serializable]
    private struct NodePrefabEntry
    {
        public BoardNodeType type;
        public NodeView prefab;
    }

    // 같은 칸에 두 팀 말이 있으면 옆으로 벌려 놓는 간격 (GameFlowController와 같은 값)
    private const float TeamSpacing = 0.55f;

    private readonly Dictionary<BoardNode, NodeView> views = new Dictionary<BoardNode, NodeView>();

    // Destroy는 프레임 끝에 지워지므로, 그 전에 다시 칸을 찾을 때 빼기 위해 기억한다
    private readonly HashSet<NodeView> destroying = new HashSet<NodeView>();

    public BoardGraph Graph { get; private set; }
    public BoardLayoutSO Layout => layout;
    public string LayoutId
    {
        get
        {
            if (layout == null) return "scene";
            const string prefix = "BoardLayout_";
            return layout.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? layout.name.Substring(prefix.Length).ToLowerInvariant()
                : layout.name.ToLowerInvariant();
        }
    }

    // 판 배치가 적용된 뒤 (길 화살표 등 화면 갱신용)
    public event Action LayoutReloaded;

    private Transform NodeRoot => nodeRoot != null ? nodeRoot : transform;

    private void Awake()
    {
        if (layout != null) ApplyLayout(layout);
        else Graph = BuildGraphFromScene();
    }

    public NodeView GetView(BoardNode node) => node != null && views.TryGetValue(node, out NodeView view) ? view : null;

    /// <summary>
    /// 서버가 nodeId로 전달한 이동 경로를 화면 위치로 변환할 때 사용한다.
    /// </summary>
    public NodeView GetView(int nodeId) => Graph == null ? null : GetView(Graph.GetNode(nodeId));

    /// <summary>
    /// 서버 게임 시작 전에 씬의 노드 ID와 연결이 사용 가능한지 확인한다.
    /// </summary>
    public bool ValidateForGameStart(string expectedLayoutId, out string error)
    {
        if (Graph == null)
        {
            error = "BoardGraph가 생성되지 않았습니다.";
            return false;
        }

        List<NodeView> nodeViews = GetNodeViews();
        if (FindStartNode(nodeViews, false) == null)
        {
            error = "Start 타입 노드는 정확히 하나여야 합니다.";
            return false;
        }

        var ids = new HashSet<int>();
        foreach (NodeView view in nodeViews)
        {
            if (view.Id < 0)
            {
                error = $"{view.name}의 노드 ID가 지정되지 않았습니다.";
                return false;
            }
            if (!ids.Add(view.Id))
            {
                error = $"노드 ID {view.Id}가 중복되었습니다.";
                return false;
            }
            if (Graph.GetNode(view.Id) == null)
            {
                error = $"노드 ID {view.Id}가 BoardGraph에 없습니다.";
                return false;
            }
        }

        if (!string.Equals(LayoutId, expectedLayoutId, StringComparison.OrdinalIgnoreCase))
        {
            error = $"보드 레이아웃이 다릅니다. Scene={LayoutId}, GameSetup={expectedLayoutId}";
            return false;
        }

        error = null;
        return true;
    }

    // 판의 모든 칸 오브젝트 (Node Root 아래, 비워 두면 씬 전체). 그래프를 만들기 전인 에디터에서도 사용 가능
    public List<NodeView> GetNodeViews()
    {
        var result = new List<NodeView>();
        if (nodeRoot != null)
        {
            result.AddRange(nodeRoot.GetComponentsInChildren<NodeView>(true));
        }
        else
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<NodeView>(true));
        }

        destroying.RemoveWhere(view => view == null); // 이미 지워진 것 정리
        result.RemoveAll(view => view == null || destroying.Contains(view));
        return result;
    }

    // 종류가 Start인 칸. 없거나 둘 이상이면 null (로그 출력)
    private NodeView FindStartNode(List<NodeView> nodeViews, bool logError = true)
    {
        List<NodeView> starts = nodeViews.FindAll(view => view.Type == BoardNodeType.Start);
        if (starts.Count == 1) return starts[0];

        if (logError) Debug.LogError($"[BoardView] 종류가 Start인 칸이 {starts.Count}개입니다. 판에 하나만 있어야 합니다.", this);
        return null;
    }

    // ---------- 판 바꾸기 ----------

    // 판을 바꾸고 말 화면을 맞춘다. 판 위의 말은 같은 번호의 칸에 그대로 남고,
    // 칸이 없어진 말은 잡힌 것처럼 대기석으로 돌아간다 (규칙 처리는 BoardState가 Graph.Rebuilt를 받아서 한다).
    // 말이 움직이는 중이면 바꾸지 않고 false. 편집 중에도 쓸 수 있다 (우클릭 메뉴 '레이아웃을 씬에 불러오기')
    public bool SwitchLayout(BoardLayoutSO newLayout) =>
        SwitchLayout(newLayout, FindObjectsByType<PieceView>(FindObjectsSortMode.None));

    public bool SwitchLayout(BoardLayoutSO newLayout, IReadOnlyList<PieceView> pieces)
    {
        foreach (PieceView piece in pieces)
        {
            if (piece.IsMoving)
            {
                Debug.LogWarning("[BoardView] 말이 움직이는 중에는 판을 바꿀 수 없습니다.", this);
                return false;
            }
        }

        var wasOnBoard = new HashSet<PieceView>();
        foreach (PieceView piece in pieces) if (piece.Piece != null && piece.Piece.IsOnBoard) wasOnBoard.Add(piece);

        if (!ApplyLayout(newLayout)) return false;

        var returned = new List<string>();
        foreach (PieceView piece in pieces)
        {
            if (!wasOnBoard.Contains(piece) || !piece.Piece.IsWaiting) continue;
            piece.ReturnHome(); // 잡힌 것처럼 대기석으로
            returned.Add(piece.name);
        }
        PlacePiecesOnBoard(pieces);

        string returnedText = returned.Count > 0 ? $" 칸이 없어져 대기석으로 돌아간 말: {string.Join(", ", returned)}" : "";
        Debug.Log($"[BoardView] {newLayout.name} 판으로 바꿨습니다.{returnedText}", this);
        return true;
    }

    // 판 위의 말을 자기 칸(번호 그대로)의 위치로 옮긴다. 같은 칸의 두 팀 말은 옆으로 벌린다
    public void PlacePiecesOnBoard(IReadOnlyList<PieceView> pieces)
    {
        var byNode = new Dictionary<BoardNode, List<PieceView>>();
        foreach (PieceView piece in pieces)
        {
            if (piece.Piece == null || !piece.Piece.IsOnBoard) continue;
            if (!byNode.TryGetValue(piece.Piece.Node, out List<PieceView> list)) byNode[piece.Piece.Node] = list = new List<PieceView>();
            list.Add(piece);
        }

        foreach (KeyValuePair<BoardNode, List<PieceView>> pair in byNode)
        {
            NodeView nodeView = GetView(pair.Key);
            if (nodeView == null) continue;

            var teams = new List<int>();
            foreach (PieceView piece in pair.Value) if (!teams.Contains(piece.Team)) teams.Add(piece.Team);
            teams.Sort();

            foreach (PieceView piece in pair.Value)
            {
                int index = teams.IndexOf(piece.Team);
                float x = teams.Count == 1 ? 0f : (index - (teams.Count - 1) * 0.5f) * TeamSpacing;
                piece.SetBoardPosition(nodeView.Position + new Vector3(x, 0f, 0f));
            }
        }
    }

    // ---------- 판 배치 적용 ----------

    // 판 배치를 적용한다. Graph가 있으면 같은 번호의 칸 데이터를 그대로 두고 모양만 바꾸고(Rebuild),
    // 칸 오브젝트는 같은 번호·같은 종류면 다시 쓰고, 종류가 다르거나 없으면 프리팹으로 만들고, 새 판에 없으면 지운다
    private bool ApplyLayout(BoardLayoutSO source)
    {
        if (source == null) return false;

        BoardDefinition definition = source.Definition;
        if (!definition.Validate(out string error))
        {
            Debug.LogError($"[BoardView] {source.name} 레이아웃이 잘못됐습니다: {error}", source);
            return false;
        }

        // 편집 중이거나 처음이면 씬의 칸으로 그래프부터 만든다 (그래야 기존 칸 오브젝트를 다시 쓸 수 있다)
        List<NodeView> sceneNodes = GetNodeViews();
        if (Graph == null && sceneNodes.Count > 0) Graph = BuildGraphFromScene();

        var existing = new Dictionary<int, NodeView>();
        foreach (NodeView view in sceneNodes)
        {
            if (existing.ContainsKey(view.Id)) DestroyNode(view);
            else existing.Add(view.Id, view);
        }

        var placed = new Dictionary<int, NodeView>();
        foreach (NodeDefinition node in definition.nodes)
        {
            if (existing.TryGetValue(node.id, out NodeView view))
            {
                existing.Remove(node.id);
                if (view.Type != node.type) { DestroyNode(view); view = null; }
            }

            if (view == null) view = CreateNode(node);
            if (view == null) return false;

            PlaceNode(view, source, node.id);
            placed.Add(node.id, view);
        }
        foreach (NodeView leftover in existing.Values) DestroyNode(leftover); // 새 판에 없는 칸

        if (Graph == null) Graph = definition.CreateGraph();
        else Graph.Rebuild(definition);

        ConfigureLinks(definition, placed);
        BindViews(placed);
        layout = source;

        // 씬에서 판을 만들 때와 같은 기준으로 시작 칸 확인 (정의 검사를 통과했으면 항상 성공해야 한다)
        NodeView start = FindStartNode(new List<NodeView>(placed.Values));
        if (start == null || start.Node != Graph.StartNode)
        {
            Debug.LogError($"[BoardView] {source.name}을(를) 불러온 뒤 시작 칸이 맞지 않습니다.", source);
            return false;
        }

        LayoutReloaded?.Invoke();
        return true;
    }

    private NodeView CreateNode(NodeDefinition node)
    {
        NodeView prefab = GetPrefab(node.type);
        if (prefab == null)
        {
            Debug.LogError($"[BoardView] {node.type} 칸 프리팹이 없습니다. Node Prefabs에 {node.type} 종류를 추가해 주세요.", this);
            return null;
        }

        NodeView view = Instantiate(prefab, NodeRoot);
        view.name = $"{prefab.name} ({node.id})";
        return view;
    }

    private NodeView GetPrefab(BoardNodeType type)
    {
        int index = nodePrefabs.FindIndex(entry => entry.type == type && entry.prefab != null);
        return index >= 0 ? nodePrefabs[index].prefab : null;
    }

    // 저장된 위치는 '보이는 원의 중심'이므로, 자식 스프라이트가 어긋난 칸도 원이 그 위치에 오도록 맞춘다
    private void PlaceNode(NodeView view, BoardLayoutSO source, int nodeId)
    {
        Vector3 local = source.TryGetPosition(nodeId, out Vector3 position) ? position : Vector3.zero;
        view.transform.localPosition = local;
        view.transform.position += NodeRoot.TransformPoint(local) - view.Position;
    }

    private static void ConfigureLinks(BoardDefinition definition, Dictionary<int, NodeView> nodeViews)
    {
        List<NodeView> ToViews(List<int> ids) => ids.ConvertAll(id => nodeViews[id]);
        foreach (NodeDefinition node in definition.nodes)
        {
            nodeViews[node.id].Configure(node.id, node.type, ToViews(node.next), ToViews(node.prev), ToViews(node.shortcut));
        }
    }

    // 칸 오브젝트 ↔ 그래프의 칸 데이터 (번호로 짝지음)
    private void BindViews(Dictionary<int, NodeView> nodeViews)
    {
        views.Clear();
        foreach (KeyValuePair<int, NodeView> pair in nodeViews)
        {
            BoardNode node = Graph.GetNode(pair.Key);
            pair.Value.Bind(node);
            views.Add(node, pair.Value);
        }
    }

    private void DestroyNode(NodeView view)
    {
        if (Application.isPlaying)
        {
            // Destroy는 프레임 끝에 지워진다. 같은 프레임에 판을 다시 바꿀 때 '다시 쓸 칸'으로 잡히지 않도록
            // 끄고, 부모에서 떼고, GetNodeViews가 빼도록 기억해 둔다
            view.gameObject.SetActive(false);
            view.transform.SetParent(null, false);
            destroying.Add(view);
            Destroy(view.gameObject);
        }
        else
        {
            DestroyImmediate(view.gameObject); // 편집 중에는 Destroy가 동작하지 않는다
        }
    }

    // ---------- 씬의 칸 → 그래프 ----------

    // 씬에 놓인 칸의 연결로 그래프를 만든다 (씬 → BoardDefinition → BoardGraph)
    private BoardGraph BuildGraphFromScene()
    {
        views.Clear();
        List<NodeView> nodeViews = GetNodeViews();
        NodeView start = FindStartNode(nodeViews);
        if (start == null) return null;

        var byId = new Dictionary<int, NodeView>();
        foreach (NodeView view in nodeViews)
        {
            if (view.Id >= 0 && byId.TryAdd(view.Id, view)) continue;

            string reason = view.Id < 0 ? "지정되지 않았습니다" : "다른 칸과 겹칩니다";
            Debug.LogError($"[BoardView] {view.name}의 칸 번호({view.Id})가 {reason}. NodeView 인스펙터의 Id에 다른 칸과 겹치지 않는 번호를 넣어 주세요.", view);
            return null;
        }

        BoardDefinition definition = CaptureDefinition(nodeViews, start);
        if (!definition.Validate(out string error))
        {
            Debug.LogError($"[BoardView] 씬의 판이 잘못됐습니다: {error}", this);
            return null;
        }

        BoardGraph graph = definition.CreateGraph();
        Graph = graph;
        BindViews(byId);
        WarnMissingPrevLinks(nodeViews, start);
        return graph;
    }

    // 칸 오브젝트들의 번호/종류/연결을 판 정의로 옮긴다
    private static BoardDefinition CaptureDefinition(List<NodeView> nodeViews, NodeView start)
    {
        List<int> ToIds(IReadOnlyList<NodeView> nodes)
        {
            var ids = new List<int>();
            foreach (NodeView node in nodes) if (node != null) ids.Add(node.Id);
            return ids;
        }

        var definition = new BoardDefinition { startNodeId = start.Id };
        foreach (NodeView view in nodeViews)
        {
            definition.nodes.Add(new NodeDefinition
            {
                id = view.Id,
                type = view.Type,
                next = ToIds(view.NextNodes),
                prev = ToIds(view.PrevNodes),
                shortcut = ToIds(view.ShortcutNodes),
            });
        }
        definition.nodes.Sort((a, b) => a.id.CompareTo(b.id));
        return definition;
    }

    // A → B로 가는 연결이 있는데 B의 Prev에 A가 없으면 빽도/방향 판정이 틀어지므로 경고
    // Start에서 나가는 연결은 제외: 첫 칸에서 빽도하면 Start가 아니라 End로 가야 하므로 첫 칸의 Prev에 Start를 넣지 않는다
    private static void WarnMissingPrevLinks(List<NodeView> nodeViews, NodeView start)
    {
        foreach (NodeView view in nodeViews)
        {
            if (view == start) continue;

            foreach (NodeView next in view.NextNodes) WarnIfMissingPrev(view, next);
            foreach (NodeView next in view.ShortcutNodes) WarnIfMissingPrev(view, next);
        }
    }

    private static void WarnIfMissingPrev(NodeView from, NodeView to)
    {
        if (to == null) return;

        bool hasPrev = false;
        foreach (NodeView prev in to.PrevNodes) if (prev == from) hasPrev = true;
        if (!hasPrev) Debug.LogWarning($"[BoardView] {from.name} → {to.name} 연결이 있지만 {to.name}의 Prev에 {from.name}이(가) 없습니다.", from);
    }

    // ---------- 에디터 메뉴 ----------

    // Layout을 씬에 적용한다 (저장한 판을 씬에서 고칠 때). 실행 중 판 바꾸기와 같은 SwitchLayout을 쓴다.
    // 주의: 없어지는 칸은 바로 지워지고 Ctrl+Z로 되돌릴 수 없다.
    // 주의: UnityEditor를 쓰지 않아 씬이 '바뀜' 상태가 되지 않는다. 불러온 뒤 씬에서 무엇이든 하나 수정해야 Ctrl+S로 저장된다
    [ContextMenu("레이아웃을 씬에 불러오기")]
    private void LoadLayoutToScene()
    {
        if (layout == null)
        {
            Debug.LogError("[BoardView] 불러올 Layout이 지정되지 않았습니다.", this);
            return;
        }

        if (SwitchLayout(layout) && !Application.isPlaying)
        {
            Debug.Log("[BoardView] 저장하려면 씬에서 무엇이든 하나 수정한 뒤 Ctrl+S를 누르세요 (그냥 Ctrl+S는 저장되지 않음).", this);
        }
    }

#if UNITY_EDITOR
    // 씬에 놓인 판을 레이아웃 에셋으로 저장. Layout이 지정돼 있으면 그 에셋을 덮어쓰고, 없으면 새로 만든다
    [ContextMenu("현재 판을 레이아웃으로 저장")]
    private void SaveLayout()
    {
        List<NodeView> nodeViews = GetNodeViews();
        NodeView start = FindStartNode(nodeViews);
        if (start == null) return;

        BoardDefinition definition = CaptureDefinition(nodeViews, start);
        if (!definition.Validate(out string error))
        {
            Debug.LogError($"[BoardView] 저장할 수 없습니다: {error} (번호가 없거나 겹치는 칸은 NodeView 인스펙터의 Id를 직접 지정)", this);
            return;
        }

        // 보이는 원의 중심을 저장 (자식 스프라이트만 옮긴 칸도 보이는 위치 그대로 복원)
        var layouts = new List<NodeLayout>();
        foreach (NodeView view in nodeViews) layouts.Add(new NodeLayout(view.Id, NodeRoot.InverseTransformPoint(view.Position)));
        layouts.Sort((a, b) => a.nodeId.CompareTo(b.nodeId));

        BoardLayoutSO target = layout;
        if (target != null && !UnityEditor.EditorUtility.DisplayDialog("레이아웃 저장", $"{target.name}을(를) 현재 판으로 덮어쓸까요?", "덮어쓰기", "새로 저장"))
        {
            target = null;
        }

        if (target == null)
        {
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject("레이아웃 저장", "BoardLayout", "asset", "판 레이아웃을 저장할 위치를 고르세요.");
            if (string.IsNullOrEmpty(path)) return;

            target = ScriptableObject.CreateInstance<BoardLayoutSO>();
            UnityEditor.AssetDatabase.CreateAsset(target, path);
        }

        UnityEditor.Undo.RecordObject(target, "Save Board Layout");
        target.Set(definition, layouts);
        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[BoardView] 칸 {definition.nodes.Count}개를 {UnityEditor.AssetDatabase.GetAssetPath(target)}에 저장했습니다.", target);
    }
#endif
}
