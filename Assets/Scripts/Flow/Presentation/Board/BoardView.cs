using System.Collections.Generic;
using UnityEngine;

// 씬의 NodeView 연결을 읽어서 BoardGraph(순수 데이터)를 만든다.
public class BoardView : MonoBehaviour
{
    [Tooltip("판 밖 출발 칸. 앞으로 가다가 이 칸에 들어오면 골인")]
    [SerializeField] private NodeView startNode;

    private readonly Dictionary<BoardNode, NodeView> views = new Dictionary<BoardNode, NodeView>();

    public BoardGraph Graph { get; private set; }

    private void Awake()
    {
        Graph = BuildGraph();
    }

    public NodeView GetView(BoardNode node) => node != null && views.TryGetValue(node, out NodeView view) ? view : null;

    // 시작 칸에서 연결된 모든 칸 (그래프를 만들기 전인 에디터에서도 사용 가능)
    public List<NodeView> GetNodeViews() => startNode != null ? CollectReachable(startNode) : new List<NodeView>();

    public BoardGraph BuildGraph()
    {
        views.Clear();
        if (startNode == null)
        {
            Debug.LogError("[BoardView] startNode가 지정되지 않았습니다.", this);
            return null;
        }

        List<NodeView> nodeViews = CollectReachable(startNode);

        // 1. 칸마다 데이터 생성 (Id = NodeView에 저장된 고정 번호)
        var nodes = new Dictionary<int, BoardNode>();
        foreach (NodeView view in nodeViews)
        {
            if (view.Id < 0 || nodes.ContainsKey(view.Id))
            {
                string reason = view.Id < 0 ? "지정되지 않았습니다" : "다른 칸과 겹칩니다";
                Debug.LogError($"[BoardView] {view.name}의 칸 번호({view.Id})가 {reason}. BoardView 우클릭 메뉴 '칸 번호 다시 매기기'를 실행해 주세요.", view);
                views.Clear();
                return null;
            }

            var node = new BoardNode(view.Id, view.Type);
            nodes.Add(view.Id, node);
            views.Add(node, view);
            view.Bind(node);
        }

        // 2. 인스펙터에 지정한 순서 그대로 연결 복사 (Prev[i] ↔ Next[i] 짝 유지)
        foreach (NodeView view in nodeViews)
        {
            CopyLinks(view.NextNodes, view.Node.NextNodes);
            CopyLinks(view.PrevNodes, view.Node.PrevNodes);
            CopyLinks(view.ShortcutNodes, view.Node.ShortcutNodes);
        }

        Validate(nodeViews);

        return new BoardGraph(nodes, startNode.Node);
    }

#if UNITY_EDITOR
    // 이동 순서대로 번호를 다시 매긴다: 시작 칸 0 → Next[0]을 따라 한 바퀴 → 지름길/갈림길 순.
    // 저장 데이터나 네트워크 프로토콜이 번호를 쓰기 시작한 뒤에는 번호가 바뀌면 호환이 깨지므로 주의
    [ContextMenu("칸 번호 다시 매기기")]
    private void AssignNodeIds()
    {
        if (startNode == null)
        {
            Debug.LogError("[BoardView] startNode가 지정되지 않았습니다.", this);
            return;
        }

        var nodeViews = new List<NodeView>();
        var visited = new HashSet<NodeView>();

        // 한 칸에서 Next[0]을 따라 이미 번호 매긴 칸을 만날 때까지 이어서 매긴다
        void Walk(NodeView view)
        {
            while (view != null && visited.Add(view))
            {
                nodeViews.Add(view);
                view = view.NextNodes.Count > 0 ? view.NextNodes[0] : null;
            }
        }

        Walk(startNode); // 바깥 길
        for (int i = 0; i < nodeViews.Count; i++) // 지름길, 다른 갈림길
        {
            foreach (NodeView next in nodeViews[i].ShortcutNodes) Walk(next);
            foreach (NodeView next in nodeViews[i].NextNodes) Walk(next);
        }
        foreach (NodeView view in CollectReachable(startNode)) // Prev로만 이어진 칸
        {
            if (visited.Add(view)) nodeViews.Add(view);
        }

        for (int i = 0; i < nodeViews.Count; i++)
        {
            var serialized = new UnityEditor.SerializedObject(nodeViews[i]);
            serialized.FindProperty("id").intValue = i;
            serialized.ApplyModifiedProperties();
        }
        Debug.Log($"[BoardView] 칸 {nodeViews.Count}개에 번호를 매겼습니다. (0 = {startNode.name})", this);
    }
#endif

    // 시작 칸에서 연결을 따라가며 닿는 칸을 전부 모은다
    private static List<NodeView> CollectReachable(NodeView start)
    {
        var result = new List<NodeView>();
        var visited = new HashSet<NodeView>();
        var queue = new Queue<NodeView>();

        void Enqueue(NodeView view)
        {
            if (view != null && visited.Add(view)) queue.Enqueue(view);
        }

        Enqueue(start);
        while (queue.Count > 0)
        {
            NodeView view = queue.Dequeue();
            result.Add(view);
            foreach (NodeView next in view.NextNodes) Enqueue(next);
            foreach (NodeView next in view.ShortcutNodes) Enqueue(next);
            foreach (NodeView prev in view.PrevNodes) Enqueue(prev);
        }
        return result;
    }

    private static void CopyLinks(IReadOnlyList<NodeView> from, List<BoardNode> to)
    {
        foreach (NodeView view in from)
        {
            if (view != null) to.Add(view.Node);
        }
    }

    // A → B로 가는 연결이 있는데 B의 Prev에 A가 없으면 빽도/방향 판정이 틀어지므로 경고
    // Start에서 나가는 연결은 제외: 첫 칸에서 빽도하면 Start가 아니라 End로 가야 하므로 첫 칸의 Prev에 Start를 넣지 않는다
    private void Validate(List<NodeView> nodeViews)
    {
        foreach (NodeView view in nodeViews)
        {
            if (view == startNode) continue;

            foreach (BoardNode next in view.Node.NextNodes) WarnIfMissingPrev(view, next);
            foreach (BoardNode next in view.Node.ShortcutNodes) WarnIfMissingPrev(view, next);
        }
    }

    private void WarnIfMissingPrev(NodeView from, BoardNode to)
    {
        if (to.PrevNodes.Contains(from.Node)) return;

        string toName = GetView(to).name;
        Debug.LogWarning($"[BoardView] {from.name} → {toName} 연결이 있지만 {toName}의 Prev에 {from.name}이(가) 없습니다.", from);
    }
}
