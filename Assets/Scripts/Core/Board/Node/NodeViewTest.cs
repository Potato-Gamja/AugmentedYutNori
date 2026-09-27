using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BoardView가 생기기 전까지 쓰는 임시 테스트 스크립트.
/// 1) 29개 BoardNode 생성·연결 (BoardGraph 대신)
/// 2) 씬의 NodeView와 id로 매칭해서 Init (BoardView 대신)
/// 3) 키보드로 칸 이동 테스트, 씬 뷰에 연결선 표시
/// </summary>
public class NodeViewTester : MonoBehaviour
{
    private BoardNode[] _nodes;
    private readonly Dictionary<int, NodeView> _views = new Dictionary<int, NodeView>();
    private BoardNode _current;

    private void Start()
    {
        _nodes = BuildNodes();

        // 씬에 있는 NodeView를 전부 찾아서 id로 매칭
        foreach (var view in FindObjectsByType<NodeView>(FindObjectsSortMode.None))
        {
            if (_views.ContainsKey(view.NodeId))
            {
                Debug.LogError($"[Tester] id {view.NodeId}가 중복됩니다.", view);
                continue;
            }
            _views.Add(view.NodeId, view);
            view.Init(_nodes[view.NodeId]);
        }

        // 빠진 id 확인
        for (int i = 0; i < _nodes.Length; i++)
            if (!_views.ContainsKey(i)) Debug.LogWarning($"[Tester] id {i}번 NodeView가 없습니다.");

        MoveTo(_nodes[0]);
        Debug.Log("[Tester] Space: 한 칸 앞으로 / S: 지름길로 출발 / B: 빽도");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;   // 키보드가 연결 안 된 경우

        if (kb.spaceKey.wasPressedThisFrame) MoveTo(_current.GetDepartureNode(useShortcut: false));
        if (kb.sKey.wasPressedThisFrame) MoveTo(_current.GetDepartureNode());
        if (kb.bKey.wasPressedThisFrame && _current.Previous.Count > 0) MoveTo(_current.Previous[0]);
    }

    /// <summary>현재 칸 강조를 옮긴다.</summary>
    private void MoveTo(BoardNode next)
    {
        if (next == null) { Debug.Log("[Tester] 더 갈 칸이 없습니다."); return; }

        if (_current != null && _views.TryGetValue(_current.Id, out var oldView)) oldView.SetHighlight(false);
        _current = next;
        if (_views.TryGetValue(_current.Id, out var newView)) newView.SetHighlight(true);

        Debug.Log($"[Tester] 현재: {_current}");
    }

    /// <summary>29개 노드 생성·연결. 나중에 BoardGraph로 대체.</summary>
    private static BoardNode[] BuildNodes()
    {
        var n = new BoardNode[29];
        for (int i = 0; i < 29; i++) n[i] = new BoardNode(i);
        n[0] = new BoardNode(0, NodeType.Start | NodeType.Finish);
        n[5] = new BoardNode(5, NodeType.Corner);
        n[10] = new BoardNode(10, NodeType.Corner);
        n[15] = new BoardNode(15, NodeType.Corner);
        n[22] = new BoardNode(22, NodeType.Center);

        for (int i = 0; i < 19; i++) n[i].SetNext(n[i + 1]);
        n[19].SetNext(n[0]);

        int[][] lines =
        {
            new[] { 5, 20, 21, 22, 27, 28, 15 },
            new[] { 10, 23, 24, 22, 25, 26, 0 },
        };
        foreach (var line in lines)
        {
            n[line[0]].SetShortcut(n[line[1]]);
            for (int i = 1; i < line.Length - 1; i++)
                if (line[i] != 22) n[line[i]].SetNext(n[line[i + 1]]);
        }
        n[22].SetNext(n[27]);
        n[22].SetShortcut(n[25]);
        return n;
    }

#if UNITY_EDITOR
    /// <summary>씬 뷰에 연결선 표시. 흰색: Next, 빨간색: Shortcut</summary>
    private void OnDrawGizmos()
    {
        var nodes = _nodes ?? BuildNodes();
        var views = new Dictionary<int, Vector3>();
        foreach(var v in FindObjectsByType<NodeView>(FindObjectsSortMode.None)) views[v.NodeId] = v.transform.position;

        foreach (var node in nodes)
        {
            if (!views.TryGetValue(node.Id, out var from)) continue;

            if (node.Next != null && views.TryGetValue(node.Next.Id, out var to))
            { Gizmos.color = Color.white; Gizmos.DrawLine(from, to); }

            if (node.Shortcut != null && views.TryGetValue(node.Shortcut.Id, out var sc))
            { Gizmos.color = Color.red; Gizmos.DrawLine(from, sc); }
        }
    }
#endif
}