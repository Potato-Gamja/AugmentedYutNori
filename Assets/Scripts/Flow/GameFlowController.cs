using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameFlowState { GameStart, AugmentSelect, Throw, Move, EndTurn, Finish }

/// <summary>Game 씬 입력과 BoardState를 연결하는 실제 게임 흐름 컨트롤러.</summary>
public class GameFlowController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new List<PieceView>();
    [SerializeField] private PathView pathView;
    [Header("Prototype Flow")]
    [SerializeField] private bool skipAugmentSelection = true;
    [SerializeField] private bool allowSkipShortcut;
    [Header("Read Only")]
    [SerializeField] private GameFlowState currentState;
    [SerializeField] private int currentTeam;
    [SerializeField] private int currentMoveCount;
    [SerializeField] private string lastYutResult;

    private readonly Dictionary<Piece, PieceView> viewByPiece = new();
    private readonly Dictionary<NodeView, BoardPath> moveTargets = new();
    private readonly HashSet<NodeView> highlighted = new();
    private BoardState state;
    private PieceView selectedPiece;
    private Camera mainCamera;
    private bool extraThrow;
    private bool isMoving;

    public GameFlowState CurrentState => currentState;
    public int CurrentTeam => currentTeam;
    public string LastYutResult => lastYutResult;

    private void Start()
    {
        mainCamera = Camera.main;
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (pathView == null) pathView = FindFirstObjectByType<PathView>();
        if (pieceViews.Count == 0) pieceViews.AddRange(FindObjectsByType<PieceView>(FindObjectsSortMode.None));
        if (boardView == null || boardView.Graph == null)
        {
            Debug.LogError("[GameFlow] BoardView/BoardGraph 연결을 확인하세요.", this);
            enabled = false;
            return;
        }

        boardView.Graph.AllowSkipShortcut = allowSkipShortcut;
        state = new BoardState(boardView.Graph);
        state.Moved += OnMoved;
        pieceViews.Sort((a, b) => a.Team != b.Team ? a.Team.CompareTo(b.Team) : string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < pieceViews.Count; i++)
        {
            Piece piece = state.AddPiece(i, pieceViews[i].Team);
            viewByPiece.Add(piece, pieceViews[i]);
            pieceViews[i].Bind(piece);
            pieceViews[i].SetSortingOrder(10);
        }
        ChangeState(GameFlowState.GameStart);
    }

    private void OnDestroy()
    {
        if (state != null) state.Moved -= OnMoved;
    }

    private void Update()
    {
        if (currentState != GameFlowState.Move || isMoving || Mouse.current == null || mainCamera == null ||
            !Mouse.current.leftButton.wasPressedThisFrame) return;
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -mainCamera.transform.position.z));
        world.z = 0f;
        HandleClick(world);
    }

    public void OnThrowButtonClicked()
    {
        if (currentState != GameFlowState.Throw) return;
        YutThrowOutcome outcome = new YutThrower(new SystemRandomProvider())
            .Throw(StandardGetResult.StickCount, new StandardGetResult());
        currentMoveCount = outcome.Result.Steps();
        extraThrow = outcome.Result.GrantsExtraThrow();
        lastYutResult = outcome.Result.DisplayName();
        Debug.Log($"[GameFlow] Team {currentTeam + 1}: {lastYutResult} ({currentMoveCount}칸)");
        ChangeState(HasMovablePiece() ? GameFlowState.Move : GameFlowState.EndTurn);
    }

    public void OnAugmentSelected(string augmentId)
    {
        if (currentState == GameFlowState.AugmentSelect) ChangeState(GameFlowState.Throw);
    }

    private void ChangeState(GameFlowState next)
    {
        currentState = next;
        if (next == GameFlowState.GameStart)
        {
            currentTeam = 0;
            ChangeState(skipAugmentSelection ? GameFlowState.Throw : GameFlowState.AugmentSelect);
        }
        else if (next == GameFlowState.Throw)
        {
            ClearSelection();
            Debug.Log($"[GameFlow] Team {currentTeam + 1}: 윷을 던져 주세요.");
        }
        else if (next == GameFlowState.Move) Debug.Log($"[GameFlow] Team {currentTeam + 1}: 말을 선택하세요.");
        else if (next == GameFlowState.EndTurn) EndTurn();
        else if (next == GameFlowState.Finish)
        {
            ClearSelection();
            Debug.Log($"[GameFlow] Team {currentTeam + 1} 승리");
        }
    }

    private void HandleClick(Vector3 world)
    {
        NodeView node = PickNode(world);
        PieceView piece = PickPiece(world);
        if (selectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !moveTargets.ContainsKey(target)) && piece != null && piece.Piece.IsOnBoard)
                target = boardView.GetView(piece.Piece.Node);
            if (target != null && moveTargets.TryGetValue(target, out BoardPath path))
            {
                Piece moving = selectedPiece.Piece;
                ClearSelection();
                state.TryMove(moving, currentMoveCount, path.Destination, out _);
                return;
            }
        }
        if (piece != null && piece.Team == currentTeam && !piece.Piece.IsFinished)
            SelectPiece(piece == selectedPiece ? null : piece);
    }

    private void SelectPiece(PieceView piece)
    {
        ClearSelection();
        selectedPiece = piece;
        if (piece == null) return;
        piece.SetSelected(true);
        foreach (BoardPath path in state.GetMovePaths(piece.Piece, currentMoveCount))
        {
            NodeView previous = boardView.GetView(path.Start);
            for (int i = 0; i < path.Nodes.Count; i++)
            {
                NodeView current = boardView.GetView(path.Nodes[i]);
                if (current == null) continue;
                current.SetHighlight(i == path.Nodes.Count - 1 ? (path.IsFinished ? Color.green : Color.red) : Color.yellow);
                highlighted.Add(current);
                if (pathView != null && previous != null) pathView.SetHighlight(previous, current, Color.yellow);
                previous = current;
            }
            NodeView destination = boardView.GetView(path.Destination);
            if (destination != null) moveTargets[destination] = path;
        }
    }

    private void OnMoved(MoveResult result)
    {
        var points = new List<Vector3>();
        foreach (BoardNode node in result.Path.Nodes) points.Add(boardView.GetView(node).Position);
        isMoving = true;
        int remaining = result.MovedPieces.Count;
        foreach (Piece piece in result.MovedPieces)
        {
            PieceView view = viewByPiece[piece];
            view.SetSortingOrder(20);
            view.PlayMove(points, result.Path.IsFinished, () => { if (--remaining == 0) FinishMove(result); });
        }
    }

    private void FinishMove(MoveResult result)
    {
        foreach (Piece captured in result.CapturedPieces) viewByPiece[captured].ReturnHome();
        foreach (PieceView view in pieceViews)
        {
            view.SetSortingOrder(10);
            view.SetStackCount(state.GetStack(view.Piece).Count);
        }
        isMoving = false;
        if (TeamFinished(currentTeam)) ChangeState(GameFlowState.Finish);
        else
        {
            if (result.CapturedPieces.Count > 0) extraThrow = true;
            ChangeState(GameFlowState.EndTurn);
        }
    }

    private void EndTurn()
    {
        if (!extraThrow) currentTeam = currentTeam == 0 ? 1 : 0;
        extraThrow = false;
        ChangeState(GameFlowState.Throw);
    }

    private bool HasMovablePiece()
    {
        foreach (PieceView view in pieceViews)
            if (view.Team == currentTeam && !view.Piece.IsFinished && state.GetMovePaths(view.Piece, currentMoveCount).Count > 0)
                return true;
        return false;
    }

    private bool TeamFinished(int team)
    {
        bool found = false;
        foreach (PieceView view in pieceViews)
        {
            if (view.Team != team) continue;
            found = true;
            if (!view.Piece.IsFinished) return false;
        }
        return found;
    }

    private PieceView PickPiece(Vector3 world)
    {
        foreach (PieceView view in pieceViews)
            if (view != null && view.gameObject.activeInHierarchy && view.Contains(world)) return view;
        return null;
    }

    private NodeView PickNode(Vector3 world)
    {
        NodeView best = null;
        float bestDistance = float.MaxValue;
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view == null) continue;
            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;
            float distance = Vector2.Distance(world, view.Position);
            if (distance <= radius && distance < bestDistance) { best = view; bestDistance = distance; }
        }
        return best;
    }

    private void ClearSelection()
    {
        if (selectedPiece != null) selectedPiece.SetSelected(false);
        selectedPiece = null;
        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        moveTargets.Clear();
        if (pathView != null) pathView.ClearHighlights();
    }
}
