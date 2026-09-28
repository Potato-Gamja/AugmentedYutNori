using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 경로 계산 / 말 이동 확인용 테스트. 플레이 모드에서
//  - 인스펙터 버튼(-1, 1~5, 직접 입력): 이동할 칸 수 선택
//  - 말 좌클릭: 그 말이 갈 수 있는 칸 표시 → 빨간(골인은 초록) 도착 칸 좌클릭: 이동
//  - 칸 좌클릭: 그 칸에서 출발한다고 가정한 경로만 표시 (이동 없음)
//  - 칸 우클릭: 칸 확인용 직전 칸(cameFrom) 지정 (방 통과 방향 확인용, 같은 칸 다시 우클릭하면 해제)
public class PJWBoardTest : MonoBehaviour
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new List<PieceView>();
    [SerializeField] private PathView pathView;
    [SerializeField] private bool allowSkipShortcut;

    [Header("Highlight Color")]
    [SerializeField] private Color startColor = Color.cyan;
    [SerializeField] private Color cameFromColor = Color.gray;
    [SerializeField] private Color pathColor = Color.yellow;
    [SerializeField] private Color destinationColor = Color.red;
    [SerializeField] private Color finishedColor = Color.green;
    [Tooltip("경로 색이 출발 칸에서 도착 칸까지 차오르는 시간(초)")]
    [SerializeField] private float fillDuration = 0.5f;

    [Header("State (읽기 전용)")]
    [SerializeField] private PieceView selectedPiece;
    [SerializeField] private NodeView selectedNode;
    [SerializeField] private NodeView cameFromNode;
    [SerializeField] private int lastMoveCount;

    private readonly HashSet<NodeView> highlighted = new HashSet<NodeView>();
    private readonly Dictionary<NodeView, BoardPath> moveTargets = new Dictionary<NodeView, BoardPath>(); // 도착 칸 → 경로
    private readonly Dictionary<Piece, PieceView> pieceToView = new Dictionary<Piece, PieceView>();
    private BoardState state;
    private Camera cam;
    private bool isMoving;

    private const int PieceSortingOrder = 10;
    private const int MovedPieceSortingOrder = 20; // 방금 움직인 말이 겹친 말 위에 보이도록

    public bool IsReady => Application.isPlaying && boardView != null && boardView.Graph != null;

    private void Start()
    {
        cam = Camera.main;
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (pathView == null) pathView = FindFirstObjectByType<PathView>();
        if (!IsReady) return;

        // 말 번호 = pieceViews 순서. 멀티플레이에서는 모든 기기가 같은 순서로 만들어야 같은 번호가 된다
        state = new BoardState(boardView.Graph);
        for (int i = 0; i < pieceViews.Count; i++)
        {
            Piece piece = state.AddPiece(i, pieceViews[i].Team);
            pieceToView.Add(piece, pieceViews[i]);
            pieceViews[i].Bind(piece);
            pieceViews[i].SetSortingOrder(PieceSortingOrder);
        }
        state.Moved += OnStateMoved;
    }

    private void OnDestroy()
    {
        if (state != null) state.Moved -= OnStateMoved;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !IsReady || isMoving) return;

        Vector3 world = ScreenToWorld(mouse.position.ReadValue());

        if (mouse.leftButton.wasPressedThisFrame)
        {
            OnLeftClick(world);
        }
        else if (mouse.rightButton.wasPressedThisFrame && selectedPiece == null)
        {
            NodeView picked = PickNode(world);
            cameFromNode = picked == cameFromNode ? null : picked;
            Refresh();
        }
    }

    private void OnLeftClick(Vector3 world)
    {
        NodeView node = PickNode(world);
        PieceView piece = PickPiece(world);

        // 1. 말을 고른 상태에서 도착 칸을 누르면 이동.
        //    말이 칸보다 커서, 도착 칸 위에 있는 말(잡을 상대 말 등)을 누른 것도 그 칸을 누른 것으로 본다
        if (selectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !moveTargets.ContainsKey(target)) && piece != null && piece.Piece.IsOnBoard)
            {
                target = boardView.GetView(piece.Piece.Node);
            }

            if (target != null && moveTargets.TryGetValue(target, out BoardPath path))
            {
                MoveSelectedPiece(path);
                return;
            }
        }

        // 2. 말 선택 (같은 말을 다시 누르면 해제)
        if (piece != null)
        {
            SelectPiece(piece == selectedPiece ? null : piece);
            selectedNode = null;
            Refresh();
            return;
        }

        // 3. 칸 선택 (경로 확인만)
        if (node != null)
        {
            SelectPiece(null);
            selectedNode = node;
            if (cameFromNode == node) cameFromNode = null;
            Refresh();
        }
    }

    public void ShowMove(int moveCount)
    {
        lastMoveCount = moveCount;
        Refresh();
    }

    // 업힌 말은 묶음 전체를 선택 표시
    private void SelectPiece(PieceView piece)
    {
        if (selectedPiece != null) SetStackSelected(selectedPiece, false);
        selectedPiece = piece;
        if (selectedPiece != null) SetStackSelected(selectedPiece, true);
    }

    private void SetStackSelected(PieceView view, bool selected)
    {
        foreach (Piece member in state.GetStack(view.Piece)) pieceToView[member].SetSelected(selected);
    }

    private void Refresh()
    {
        ClearOutlines();
        moveTargets.Clear();

        string label;

        if (selectedPiece != null)
        {
            label = selectedPiece.name;
            if (!selectedPiece.Piece.IsWaiting) SetOutline(boardView.GetView(selectedPiece.Piece.Node), startColor);
        }
        else if (selectedNode != null)
        {
            label = selectedNode.name;
            if (cameFromNode != null) SetOutline(cameFromNode, cameFromColor);
            SetOutline(selectedNode, startColor);
        }
        else
        {
            return;
        }

        if (lastMoveCount == 0)
        {
            if (selectedPiece != null) Debug.Log("[BoardTest] 인스펙터에서 이동할 칸 수 버튼을 먼저 눌러 주세요.");
            return;
        }

        boardView.Graph.AllowSkipShortcut = allowSkipShortcut;

        // 말: 규칙(증강 포함)이 적용된 경로 / 칸: 판 모양만 보는 경로 확인용
        List<BoardPath> paths = selectedPiece != null
            ? state.GetMovePaths(selectedPiece.Piece, lastMoveCount)
            : boardView.Graph.GetMovePaths(selectedNode.Node, cameFromNode != null ? cameFromNode.Node : null, lastMoveCount);

        if (paths.Count == 0)
        {
            Debug.Log($"[BoardTest] {label}에서 {lastMoveCount}칸: 갈 수 있는 곳 없음");
            return;
        }

        foreach (BoardPath path in paths)
        {
            // 길은 pathColor, 칸 테두리는 지나가는 칸 = pathColor / 도착 칸 = destination(골인은 finished)
            // 말을 골랐을 때만 출발 칸부터 색이 차오르고, 칸만 눌러 확인할 때는 바로 표시
            Color arriveColor = path.IsFinished ? finishedColor : destinationColor;
            var fillNodes = new List<NodeView> { boardView.GetView(path.Start) };
            foreach (BoardNode node in path.Nodes) fillNodes.Add(boardView.GetView(node));

            int lastIndex = path.Nodes.Count - 1;
            void OnReached(int i) => SetOutline(fillNodes[i + 1], i == lastIndex ? arriveColor : pathColor);

            if (pathView != null && selectedPiece != null)
            {
                pathView.PlayFill(fillNodes, pathColor, fillDuration, OnReached);
            }
            else
            {
                for (int i = 0; i <= lastIndex; i++)
                {
                    OnReached(i);
                    if (pathView != null) pathView.SetHighlight(fillNodes[i], fillNodes[i + 1], pathColor);
                }
            }

            NodeView destination = boardView.GetView(path.Destination);
            if (selectedPiece != null) moveTargets[destination] = path;

            Debug.Log($"[BoardTest] {label}에서 {lastMoveCount}칸: {Describe(path)}");
        }
    }

    // 이동 요청만 한다. 실제 이동은 BoardState가 검증 후 적용하고, 화면은 Moved 이벤트(OnStateMoved)에서 갱신
    private void MoveSelectedPiece(BoardPath path)
    {
        Piece piece = selectedPiece.Piece;
        SelectPiece(null);
        ClearOutlines();
        moveTargets.Clear();

        if (!state.TryMove(piece, lastMoveCount, path.Destination, out MoveResult result))
        {
            Debug.LogWarning($"[BoardTest] 규칙상 이동할 수 없습니다: {piece}");
            return;
        }
        Debug.Log($"[BoardTest] 보낼 명령: {result.ToCommand()}"); // 멀티플레이에서는 이 값을 전송
    }

    // 로컬 입력이든 네트워크 명령(state.TryApply)이든 이동이 적용되면 여기서 애니메이션
    private void OnStateMoved(MoveResult result)
    {
        var points = new List<Vector3>();
        foreach (BoardNode node in result.Path.Nodes) points.Add(boardView.GetView(node).Position);

        // 업힌 말까지 같은 경로로 같이 이동
        foreach (PieceView pieceView in pieceViews) pieceView.SetSortingOrder(PieceSortingOrder);

        isMoving = true;
        int remaining = result.MovedPieces.Count;
        foreach (Piece moved in result.MovedPieces)
        {
            PieceView movedView = pieceToView[moved];
            movedView.SetSortingOrder(MovedPieceSortingOrder);
            movedView.PlayMove(points, result.Path.IsFinished, () =>
            {
                if (--remaining > 0) return;
                OnMoveFinished(result);
            });
        }
    }

    private void OnMoveFinished(MoveResult result)
    {
        isMoving = false;

        string stack = result.MovedPieces.Count >= 2 ? $" ({result.MovedPieces.Count}개 업힘)" : "";
        Debug.Log($"[BoardTest] {pieceToView[result.Piece].name}{stack} 이동 완료: {Describe(result.Path)}");

        foreach (Piece captured in result.CapturedPieces)
        {
            PieceView capturedView = pieceToView[captured];
            Debug.Log($"[BoardTest] {capturedView.name} 잡혔습니다.");
            capturedView.ReturnHome();
        }

        RefreshStackCounts();
    }

    private void RefreshStackCounts()
    {
        foreach (PieceView pieceView in pieceViews)
        {
            pieceView.SetStackCount(state.GetStack(pieceView.Piece).Count);
        }
    }

    private string Describe(BoardPath path)
    {
        var names = new List<string>();
        foreach (BoardNode node in path.Nodes) names.Add(boardView.GetView(node).name);

        string cameFrom = path.CameFrom != null ? boardView.GetView(path.CameFrom).name : "없음";
        return $"[{string.Join(" → ", names)}]{(path.IsFinished ? " (골인)" : "")} / 도착 후 직전 칸: {cameFrom}";
    }

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        world.z = 0f;
        return world;
    }

    private PieceView PickPiece(Vector3 world)
    {
        foreach (PieceView piece in pieceViews)
        {
            if (piece != null && !piece.Piece.IsFinished && piece.Contains(world)) return piece;
        }
        return null;
    }

    // 마우스 위치에서 가장 가까운 칸 (칸 스프라이트 반지름 안쪽만)
    private NodeView PickNode(Vector3 world)
    {
        NodeView best = null;
        float bestDistance = float.MaxValue;
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;

            Vector3 center = view.Position;
            center.z = 0f;
            float distance = Vector3.Distance(world, center);
            if (distance <= radius && distance < bestDistance)
            {
                best = view;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void SetOutline(NodeView view, Color color)
    {
        if (view == null) return;

        view.SetHighlight(color);
        highlighted.Add(view);
    }

    private void ClearOutlines()
    {
        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        if (pathView != null) pathView.ClearHighlights();
    }
}