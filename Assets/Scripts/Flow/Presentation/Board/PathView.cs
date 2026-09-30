using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 칸 사이의 길(Next / Shortcut 연결)을 Game 화면에 그린다.
// World Space Canvas 아래에 두고, 칸 좌표를 이 RectTransform 기준으로 바꿔 선과 화살표를 메시로 만든다.
// 캔버스 Sorting Order는 칸 테두리(-1)보다 낮게(-2) 둬야 화살표가 테두리를 가리지 않는다.
// SetHighlight로 지정한 길은 그 색으로, PlayFill로 지정한 경로는 출발 칸부터 색이 차오르고, 나머지는 lineColor로 그린다.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class PathView : Graphic
{
    [SerializeField] private BoardView boardView;

    [Header("Line")]
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private Color lineColor = Color.white;

    [Header("Arrow")]
    [SerializeField] private bool drawArrow = true;
    [SerializeField] private float arrowLength = 0.2f;
    [SerializeField] private float arrowWidth = 0.18f;
    [Tooltip("화살표 끝을 도착 칸 중심에서 이만큼 떨어뜨린다 (칸 원에 가려지지 않게)")]
    [SerializeField] private float arrowOffset = 0.28f;

    [Header("Start")]
    [Tooltip("Start 칸에서 나가는 길을 Start 대신 Start 직전 칸(End)에서 그린다. Start 칸을 판에서 숨길 때 사용")]
    [SerializeField] private bool drawStartLinksFromEnd = true;

    // 두 칸 사이 길 → 강조 색 (방향 상관없이, 빽도 경로도 같은 길을 칠함)
    private readonly Dictionary<(NodeView, NodeView), Color> highlights = new Dictionary<(NodeView, NodeView), Color>();

    // 차오르는 중인 경로들
    private readonly List<Fill> fills = new List<Fill>();

    private class Fill
    {
        public List<NodeView> Nodes;    // 출발 칸 + 지나가는 칸들
        public Color Color;
        public float Duration;
        public float Elapsed;
        public int ReachedCount;        // 색이 닿은 칸 수 (출발 칸 제외)
        public Action<int> OnNodeReached;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false; // 판 클릭을 가로막지 않도록
        if (boardView != null) boardView.LayoutReloaded += Refresh; // 실행 중 판이 바뀌면 다시 그린다
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        if (boardView != null) boardView.LayoutReloaded -= Refresh;
        base.OnDisable();
    }

    private void Update()
    {
        // 에디터에서 칸을 옮기면 바로 다시 그린다 (플레이 중에는 칸이 움직일 때만 Refresh 호출)
        if (!Application.isPlaying) SetVerticesDirty();

        if (fills.Count > 0) UpdateFills(Time.deltaTime);
    }

    public void Refresh() => SetVerticesDirty();

    // nodes[0](출발 칸)부터 마지막 칸까지 duration초 동안 색이 차오른다.
    // 색이 i번째 칸(nodes[i + 1])에 닿으면 onNodeReached(i) 호출
    public void PlayFill(IReadOnlyList<NodeView> nodes, Color fillColor, float duration, Action<int> onNodeReached = null)
    {
        if (nodes == null || nodes.Count < 2) return;

        // 판 밖(Start)에서 출발하는 경로는 End에서부터 차오르게 (Start → 첫 칸 길을 End → 첫 칸으로 그리므로)
        var fillNodes = new List<NodeView>(nodes);
        if (fillNodes[0] != null) fillNodes[0] = DisplayFrom(fillNodes[0]);

        fills.Add(new Fill
        {
            Nodes = fillNodes,
            Color = fillColor,
            Duration = Mathf.Max(0.0001f, duration),
            OnNodeReached = onNodeReached,
        });
        SetVerticesDirty();
    }

    // 다 찬 경로는 ClearHighlights 전까지 그 색으로 남는다
    private void UpdateFills(float deltaTime)
    {
        bool changed = false;

        // 콜백에서 ClearHighlights가 불려도 안전하도록 복사본으로 돈다
        foreach (Fill fill in fills.ToArray())
        {
            if (fill.Elapsed >= fill.Duration) continue;

            changed = true;
            fill.Elapsed = Mathf.Min(fill.Elapsed + deltaTime, fill.Duration);

            float filled = FilledLength(fill);
            float walked = 0f;
            for (int i = 0; i < fill.Nodes.Count - 1; i++)
            {
                walked += VisibleLength(fill.Nodes[i], fill.Nodes[i + 1]);
                if (i < fill.ReachedCount) continue;
                if (filled + 0.0001f < walked) break;

                fill.ReachedCount = i + 1;
                fill.OnNodeReached?.Invoke(i);
                if (!fills.Contains(fill)) break; // 콜백에서 지워졌으면 중단
            }
        }
        if (changed) SetVerticesDirty();
    }

    // 일정한 속도로 차오르도록 시간에 비례
    private float FilledLength(Fill fill) => TotalLength(fill) * (fill.Elapsed / fill.Duration);

    private float TotalLength(Fill fill)
    {
        float total = 0f;
        for (int i = 0; i < fill.Nodes.Count - 1; i++) total += VisibleLength(fill.Nodes[i], fill.Nodes[i + 1]);
        return total;
    }

    // 칸 원에 가려지지 않고 보이는 길이 = 출발 칸 가장자리 ~ 화살표 끝(도착 칸 가장자리)
    private float VisibleLength(NodeView from, NodeView to)
    {
        if (from == null || to == null) return 0f;

        float distance = Vector2.Distance(ToLocal(from.Position), ToLocal(to.Position));
        return Mathf.Max(0f, distance - arrowOffset * 2f);
    }

    // a와 b 사이 길을 지정한 색으로 칠한다
    public void SetHighlight(NodeView a, NodeView b, Color highlightColor)
    {
        if (a == null || b == null) return;

        highlights[Key(a, b)] = highlightColor;
        SetVerticesDirty();
    }

    public void ClearHighlights()
    {
        if (highlights.Count == 0 && fills.Count == 0) return;

        highlights.Clear();
        fills.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (boardView == null) return;

        var highlighted = new List<(NodeView from, NodeView to, Color color)>();

        foreach (NodeView node in boardView.GetNodeViews())
        {
            foreach (NodeView next in node.NextNodes) AddOrDefer(vh, node, next, highlighted);
            foreach (NodeView next in node.ShortcutNodes) AddOrDefer(vh, node, next, highlighted);
        }

        // 강조된 길은 다른 길에 가려지지 않도록 마지막에 그린다
        foreach (var link in highlighted) AddLink(vh, link.from, link.to, link.color);

        // 차오르는 경로는 흰 길 위에 덧그린다
        foreach (Fill fill in fills) AddFill(vh, fill);
    }

    private void AddFill(VertexHelper vh, Fill fill)
    {
        float remaining = FilledLength(fill);
        for (int i = 0; i < fill.Nodes.Count - 1 && remaining > 0f; i++)
        {
            NodeView from = fill.Nodes[i];
            NodeView to = fill.Nodes[i + 1];
            if (from == null || to == null) continue;

            float visible = VisibleLength(from, to);
            if (remaining >= visible)
            {
                AddLink(vh, from, to, fill.Color); // 다 찬 칸 사이
            }
            else
            {
                AddPartialLink(vh, from, to, remaining, fill.Color); // 차오르는 중인 칸 사이
            }
            remaining -= visible;
        }
    }

    // 출발 칸 가장자리부터 filled 길이만큼 그린다. 선을 다 채우면 화살표도 끝을 향해 이어서 차오른다
    private void AddPartialLink(VertexHelper vh, NodeView from, NodeView to, float filled, Color linkColor)
    {
        Vector2 start = ToLocal(from.Position);
        Vector2 end = ToLocal(to.Position);
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        Color32 c = linkColor * color;

        Vector2 visibleStart = start + dir * arrowOffset;
        float lineLength = VisibleLength(from, to) - (drawArrow ? arrowLength : 0f);
        float halfLine = lineWidth * 0.5f;

        // 선 부분
        float lineFilled = Mathf.Min(filled, lineLength);
        if (lineFilled > 0f)
        {
            // 칸 중심부터 그려서 이전 칸 사이와 이어지게 (앞부분은 칸 원에 가려짐)
            AddQuad(vh, start, visibleStart + dir * lineFilled, normal * halfLine, c);
        }

        // 화살표 부분: 밑변(전체 폭)에서 끝으로 갈수록 좁아지는 사다리꼴
        float arrowFilled = filled - lineLength;
        if (!drawArrow || arrowFilled <= 0f) return;

        Vector2 arrowBase = visibleStart + dir * lineLength;
        float ratio = Mathf.Clamp01(arrowFilled / arrowLength);
        float halfBase = arrowWidth * 0.5f;
        float halfCut = halfBase * (1f - ratio);
        AddTrapezoid(vh, arrowBase, arrowBase + dir * (arrowLength * ratio), normal, halfBase, halfCut, c);
    }

    private static void AddTrapezoid(VertexHelper vh, Vector2 a, Vector2 b, Vector2 normal, float halfA, float halfB, Color32 c)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a - normal * halfA, c, Vector2.zero);
        vh.AddVert(a + normal * halfA, c, Vector2.zero);
        vh.AddVert(b + normal * halfB, c, Vector2.zero);
        vh.AddVert(b - normal * halfB, c, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private void AddOrDefer(VertexHelper vh, NodeView from, NodeView to, List<(NodeView, NodeView, Color)> highlighted)
    {
        if (from == null || to == null) return;

        // Start → 첫 칸은 End → 첫 칸으로 바꿔 그린다. 강조는 원래 길(Start-첫 칸)이나 바뀐 길(End-첫 칸) 어느 쪽으로 지정해도 적용
        NodeView drawFrom = DisplayFrom(from);
        if (highlights.TryGetValue(Key(from, to), out Color highlightColor) ||
            highlights.TryGetValue(Key(drawFrom, to), out highlightColor))
        {
            highlighted.Add((drawFrom, to, highlightColor));
            return;
        }
        AddLink(vh, drawFrom, to, lineColor);
    }

    // 화면에 그릴 출발 칸. Start에서 나가는 길이면 Start 직전 칸(End)
    private NodeView DisplayFrom(NodeView from)
    {
        if (!drawStartLinksFromEnd || from.Type != BoardNodeType.Start) return from;
        return from.PrevNodes.Count > 0 && from.PrevNodes[0] != null ? from.PrevNodes[0] : from;
    }

    private void AddLink(VertexHelper vh, NodeView from, NodeView to, Color linkColor)
    {
        Vector2 start = ToLocal(from.Position);
        Vector2 end = ToLocal(to.Position);
        Vector2 delta = end - start;
        if (delta.sqrMagnitude < 0.0001f) return;

        Vector2 dir = delta.normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        Color32 c = linkColor * color; // Graphic.color로 전체 색/투명도 조절

        if (!drawArrow)
        {
            AddQuad(vh, start, end, normal * lineWidth * 0.5f, c);
            return;
        }

        // 선은 화살표 뒤까지만, 화살표 끝은 도착 칸 가장자리 앞
        Vector2 tip = end - dir * arrowOffset;
        Vector2 arrowBase = tip - dir * arrowLength;
        AddQuad(vh, start, arrowBase, normal * lineWidth * 0.5f, c);
        AddTriangle(vh, tip, arrowBase + normal * arrowWidth * 0.5f, arrowBase - normal * arrowWidth * 0.5f, c);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 halfWidth, Color32 c)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a - halfWidth, c, Vector2.zero);
        vh.AddVert(a + halfWidth, c, Vector2.zero);
        vh.AddVert(b + halfWidth, c, Vector2.zero);
        vh.AddVert(b - halfWidth, c, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c2, Color32 c)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero);
        vh.AddVert(b, c, Vector2.zero);
        vh.AddVert(c2, c, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
    }

    // 방향과 상관없이 같은 길이면 같은 키
    private static (NodeView, NodeView) Key(NodeView a, NodeView b) =>
        a.GetInstanceID() < b.GetInstanceID() ? (a, b) : (b, a);

    private Vector2 ToLocal(Vector3 worldPosition) => rectTransform.InverseTransformPoint(worldPosition);
}
