using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BoardGraph·BoardView·YutThrower가 생기기 전까지 쓰는 임시 테스트 스크립트.
/// 숫자키로 윷 결과를 넣으면 규칙대로 이동한다.
/// (이동 규칙은 원래 BoardGraph.GetPath 담당이라, 여기 있는 건 테스트용 간이 버전)
/// </summary>
public class NodeViewTester : MonoBehaviour
{
    private const int CenterId = 22;
    private const int DiagonalBLastBeforeCenter = 24;
    private const int DiagonalBAfterCenter = 25;

    [Header("임시 말 (현재 위치 표시용, 아무 오브젝트나 연결)")]
    [SerializeField] private Transform pieceMarker;

    private BoardNode[] _nodes;
    private readonly Dictionary<int, NodeView> _views = new Dictionary<int, NodeView>();

    private BoardNode _current;
    private readonly List<BoardNode> _trail = new List<BoardNode>();   // 지나온 칸 기록 (빽도용)
    private bool _lapStarted;
    private bool _finished;

    // 미리보기 상태 (확정 전)
    private List<BoardNode> _previewPath;
    private bool _previewFinish;
    private bool _previewIsBack;
    private string _previewName;

    private void Start()
    {
        _nodes = BuildNodes();

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

        for (int i = 0; i < _nodes.Length; i++)
            if (!_views.ContainsKey(i)) Debug.LogWarning($"[Tester] id {i}번 NodeView가 없습니다.");

        ResetPiece();
        Debug.Log("[Tester] 1~5: 도~모 / 0 또는 -: 빽도 / Space·Enter: 확정 / Esc: 취소 / R: 리셋");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) Preview(1, "도");
        if (kb.digit2Key.wasPressedThisFrame) Preview(2, "개");
        if (kb.digit3Key.wasPressedThisFrame) Preview(3, "걸");
        if (kb.digit4Key.wasPressedThisFrame) Preview(4, "윷");
        if (kb.digit5Key.wasPressedThisFrame) Preview(5, "모");
        if (kb.digit0Key.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame) PreviewBack();

        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Confirm();
        if (kb.escapeKey.wasPressedThisFrame) CancelPreview();
        if (kb.rKey.wasPressedThisFrame) ResetPiece();
    }

    // ───────── 미리보기 ─────────

    /// <summary>윷 결과만큼 갈 경로를 계산해서 강조만 한다 (아직 이동 안 함)</summary>
    private void Preview(int steps, string yutName)
    {
        if (_finished) { Debug.Log("[Tester] 이미 완주했습니다. R로 리셋하세요."); return; }

        ClearHighlights();
        _previewPath = CalculatePath(steps, out _previewFinish);
        _previewIsBack = false;
        _previewName = yutName;

        foreach (var node in _previewPath) Highlight(node, true);

        string end = _previewFinish ? "완주" : $"{_previewPath[_previewPath.Count - 1].Id}번 도착";
        Debug.Log($"[Tester] {yutName}({steps}) 미리보기 : {PathToString(_previewPath)} → {end} (Space로 확정)");
    }

    /// <summary>빽도: 되돌아갈 칸 1개를 강조</summary>
    private void PreviewBack()
    {
        if (_finished) { Debug.Log("[Tester] 이미 완주했습니다. R로 리셋하세요."); return; }
        if (_trail.Count < 2) { Debug.Log("[Tester] 빽도: 출발 전이라 움직일 수 없습니다."); return; }

        ClearHighlights();
        var back = _trail[_trail.Count - 2];
        _previewPath = new List<BoardNode> { back };
        _previewFinish = false;
        _previewIsBack = true;
        _previewName = "빽도";

        Highlight(back, true);
        Debug.Log($"[Tester] 빽도(-1) 미리보기 : {back.Id}번 (Space로 확정)");
    }

    private void CancelPreview()
    {
        if (_previewPath == null) return;
        ClearHighlights();
        _previewPath = null;
        Debug.Log("[Tester] 미리보기 취소");
    }

    // ───────── 확정 ─────────

    private void Confirm()
    {
        if (_previewPath == null) { Debug.Log("[Tester] 먼저 윷 결과(1~5, 0)를 입력하세요."); return; }

        ClearHighlights();

        if (_previewIsBack)
        {
            _trail.RemoveAt(_trail.Count - 1);
            SetCurrent(_trail[_trail.Count - 1]);
        }
        else if (_previewFinish)
        {
            _finished = true;
            if (pieceMarker != null) pieceMarker.gameObject.SetActive(false);   // 판에서 나감
            Debug.Log($"[Tester] {_previewName} 확정 → 완주!");
            _previewPath = null;
            return;
        }
        else
        {
            if (_current.IsStart) _lapStarted = true;
            _trail.AddRange(_previewPath);
            SetCurrent(_previewPath[_previewPath.Count - 1]);
        }

        Debug.Log($"[Tester] {_previewName} 확정 → 현재 {_current.Id}번");
        _previewPath = null;
    }

    // ───────── 경로 계산 ─────────

    /// <summary>
    /// 현재 칸에서 steps만큼 갈 경로를 계산한다. 상태는 바꾸지 않는다.
    /// 도착점을 지나치면 finish = true, 경로는 도착점까지만 담는다.
    /// </summary>
    private List<BoardNode> CalculatePath(int steps, out bool finish)
    {
        finish = false;
        var path = new List<BoardNode>();
        BoardNode cur = _current;
        BoardNode came = _trail.Count >= 2 ? _trail[_trail.Count - 2] : null;
        bool lapStarted = _lapStarted;

        for (int i = 0; i < steps; i++)
        {
            // 출발점을 떠난 뒤 도착점에서 한 칸 더 나아가면 완주
            if (lapStarted && cur.IsFinish) { finish = true; break; }

            BoardNode next;
            if (i == 0)
                next = cur.GetDepartureNode();
            else if (cur.Id == CenterId && came != null && came.Id == DiagonalBLastBeforeCenter)
                next = _nodes[DiagonalBAfterCenter];
            else
                next = cur.Next;

            if (cur.IsStart) lapStarted = true;
            came = cur;
            cur = next;
            path.Add(cur);
        }
        return path;
    }

    // ───────── 공통 ─────────

    private void ResetPiece()
    {
        ClearHighlights();
        _previewPath = null;
        _trail.Clear();
        _trail.Add(_nodes[0]);
        _lapStarted = false;
        _finished = false;
        if (pieceMarker != null) pieceMarker.gameObject.SetActive(true);
        SetCurrent(_nodes[0]);
        Debug.Log("[Tester] 리셋: 0번(출발)");
    }

    /// <summary>현재 칸을 바꾸고 말 마커를 그 칸으로 옮긴다</summary>
    private void SetCurrent(BoardNode node)
    {
        _current = node;
        if (pieceMarker != null && _views.TryGetValue(node.Id, out var view))
            pieceMarker.position = view.Position;
    }

    private void Highlight(BoardNode node, bool on)
    {
        if (_views.TryGetValue(node.Id, out var view)) view.SetHighlight(on);
    }

    private void ClearHighlights()
    {
        foreach (var v in _views.Values) v.SetHighlight(false);
    }

    private static string PathToString(List<BoardNode> path)
    {
        var ids = new List<string>();
        foreach (var n in path) ids.Add(n.Id.ToString());
        return ids.Count > 0 ? string.Join(" → ", ids) : "-";
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
    [SerializeField] private float boardSize = 10f;

    [ContextMenu("Auto Place Nodes")]
    private void AutoPlaceNodes()
    {
        float h = boardSize / 2f;
        float s = boardSize / 5f;
        var pos = new Vector2[29];

        for (int i = 0; i <= 5; i++) pos[i] = new Vector2(h, -h + s * i);
        for (int i = 1; i <= 5; i++) pos[5 + i] = new Vector2(h - s * i, h);
        for (int i = 1; i <= 5; i++) pos[10 + i] = new Vector2(-h, h - s * i);
        for (int i = 1; i <= 4; i++) pos[15 + i] = new Vector2(-h + s * i, -h);

        float d1 = h * 2f / 3f, d2 = h / 3f;
        pos[20] = new Vector2(d1, d1); pos[21] = new Vector2(d2, d2);
        pos[22] = Vector2.zero;
        pos[27] = new Vector2(-d2, -d2); pos[28] = new Vector2(-d1, -d1);
        pos[23] = new Vector2(-d1, d1); pos[24] = new Vector2(-d2, d2);
        pos[25] = new Vector2(d2, -d2); pos[26] = new Vector2(d1, -d1);

        foreach (var v in FindObjectsByType<NodeView>(FindObjectsSortMode.None))
        {
            if (v.NodeId < 0 || v.NodeId >= 29) continue;
            UnityEditor.Undo.RecordObject(v.transform, "Auto Place Nodes");
            v.transform.position = pos[v.NodeId];
        }
    }

    /// <summary>씬 뷰에 연결선 표시. 흰색: Next, 빨간색: Shortcut</summary>
    private void OnDrawGizmos()
    {
        var nodes = _nodes ?? BuildNodes();
        var views = new Dictionary<int, Vector3>();
        foreach (var v in FindObjectsByType<NodeView>(FindObjectsSortMode.None)) views[v.NodeId] = v.transform.position;

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