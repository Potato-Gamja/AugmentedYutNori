using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameFlowState { GameStart, AugmentSelect, Throw, Move, EndTurn, Finish }

/// <summary>저장된 윷 결과를 말 선택 후 도착 노드를 눌러 소비하는 게임 흐름.</summary>
public class GameFlowController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new();
    [SerializeField] private PathView pathView;
    [SerializeField] private TMP_Text yutResultText;
    [SerializeField] private TMP_Text turnText;
    [Header("Prototype Flow")]
    [SerializeField] private bool skipAugmentSelection = true;
    [SerializeField] private bool allowSkipShortcut;
    [Header("Read Only")]
    [SerializeField] private GameFlowState currentState;
    [SerializeField] private int currentTeam;
    [SerializeField] private string lastYutResult;

    private sealed class MoveOption
    {
        public YutResult Result;
        public BoardPath Path;
    }

    private readonly Dictionary<Piece, PieceView> viewByPiece = new();
    private readonly Dictionary<NodeView, List<MoveOption>> moveTargets = new();
    private readonly HashSet<NodeView> highlighted = new();
    private readonly List<GameObject> resultLabels = new();
    private readonly List<YutResult> storedResults = new();
    private BoardState state;
    private PieceView selectedPiece;
    private Camera mainCamera;
    private bool isMoving;

    public GameFlowState CurrentState => currentState;
    public int CurrentTeam => currentTeam;
    public string LastYutResult => lastYutResult;
    public IReadOnlyList<YutResult> StoredResults => storedResults;

    private void Start()
    {
        mainCamera = Camera.main;
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (pathView == null) pathView = FindFirstObjectByType<PathView>();
        if (yutResultText == null) yutResultText = FindText("YutResultText");
        if (turnText == null) turnText = FindText("TurnText");
        foreach (PieceView view in FindObjectsByType<PieceView>(FindObjectsSortMode.None))
            if (!pieceViews.Contains(view)) pieceViews.Add(view);

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
        storedResults.Add(outcome.Result);
        lastYutResult = outcome.Result.DisplayName();
        RefreshStoredResultText();
        Debug.Log($"[GameFlow] Team {currentTeam + 1}: {lastYutResult} 저장 ({outcome.Result.Steps()}칸)");

        if (outcome.Result.GrantsExtraThrow())
        {
            if (turnText != null) turnText.text = $"Team {currentTeam + 1} Turn  |  Throw Again";
            return;
        }
        BeginMoveSelectionOrEndTurn();
    }

    public void OnAugmentSelected(string augmentId)
    {
        if (currentState == GameFlowState.AugmentSelect) ChangeState(GameFlowState.Throw);
    }

    private void BeginMoveSelectionOrEndTurn()
    {
        if (HasAnyStoredMove()) ChangeState(GameFlowState.Move);
        else
        {
            Debug.Log("[GameFlow] 저장된 결과로 이동할 수 없어 턴을 종료합니다.");
            storedResults.Clear();
            RefreshStoredResultText();
            ChangeState(GameFlowState.EndTurn);
        }
    }

    private void ChangeState(GameFlowState next)
    {
        currentState = next;
        if (next == GameFlowState.GameStart)
        {
            currentTeam = 0;
            storedResults.Clear();
            ChangeState(skipAugmentSelection ? GameFlowState.Throw : GameFlowState.AugmentSelect);
        }
        else if (next == GameFlowState.Throw)
        {
            ClearSelection();
            RefreshTurnHighlights();
            if (turnText != null)
                turnText.text = storedResults.Count > 0
                    ? $"Team {currentTeam + 1} Turn  |  Bonus Throw"
                    : $"Team {currentTeam + 1} Turn  |  Throw Yut";
        }
        else if (next == GameFlowState.Move)
        {
            ClearSelection();
            RefreshTurnHighlights();
            if (turnText != null) turnText.text = $"Team {currentTeam + 1} Turn  |  Select a Piece";
        }
        else if (next == GameFlowState.EndTurn) EndTurn();
        else if (next == GameFlowState.Finish)
        {
            ClearSelection();
            RefreshTurnHighlights(false);
            if (turnText != null) turnText.text = $"Team {currentTeam + 1} Wins!";
        }
    }

    private void HandleClick(Vector3 world)
    {
        NodeView node = PickNode(world);
        PieceView clickedPiece = PickPiece(world);

        if (selectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !moveTargets.ContainsKey(target)) && clickedPiece != null && clickedPiece.Piece.IsOnBoard)
                target = boardView.GetView(clickedPiece.Piece.Node);

            if (target != null && moveTargets.TryGetValue(target, out List<MoveOption> options) && options.Count > 0)
            {
                MoveUsingOption(options[0]);
                return;
            }
        }

        if (clickedPiece != null && clickedPiece.Team == currentTeam && !clickedPiece.Piece.IsFinished)
            SelectPiece(clickedPiece == selectedPiece ? null : clickedPiece);
    }

    private void SelectPiece(PieceView piece)
    {
        ClearSelection();
        selectedPiece = piece;
        if (piece == null) return;
        piece.SetSelected(true);

        foreach (YutResult result in storedResults)
        {
            foreach (BoardPath path in state.GetMovePaths(piece.Piece, result.Steps()))
            {
                NodeView destination = boardView.GetView(path.Destination);
                if (destination == null) continue;
                if (!moveTargets.TryGetValue(destination, out List<MoveOption> options))
                {
                    options = new List<MoveOption>();
                    moveTargets.Add(destination, options);
                }
                options.Add(new MoveOption { Result = result, Path = path });
            }
        }

        foreach (KeyValuePair<NodeView, List<MoveOption>> pair in moveTargets)
        {
            Color color = pair.Value.Count == 1 ? GetResultColor(pair.Value[0].Result) : Color.white;
            pair.Key.SetHighlight(color);
            highlighted.Add(pair.Key);
            CreateDestinationLabel(pair.Key, pair.Value);
        }
    }

    private void MoveUsingOption(MoveOption option)
    {
        Piece moving = selectedPiece.Piece;
        int moveCount = option.Result.Steps();
        BoardNode destination = option.Path.Destination;
        storedResults.Remove(option.Result);
        RefreshStoredResultText();
        ClearSelection();
        if (!state.TryMove(moving, moveCount, destination, out _))
            Debug.LogWarning("[GameFlow] 선택한 결과로 이동할 수 없습니다.");
    }

    private void CreateDestinationLabel(NodeView destination, IReadOnlyList<MoveOption> options)
    {
        var labelObject = new GameObject("MoveResultLabel");
        labelObject.transform.SetParent(destination.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 54;
        text.characterSize = 0.055f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        var names = new List<string>();
        foreach (MoveOption option in options)
        {
            string name = GetResultLabel(option.Result);
            if (!names.Contains(name)) names.Add(name);
        }
        text.text = string.Join("/", names);
        text.color = options.Count == 1 ? GetResultColor(options[0].Result) : Color.white;
        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        renderer.sortingOrder = 35;
        resultLabels.Add(labelObject);
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
        else if (result.CapturedPieces.Count > 0) ChangeState(GameFlowState.Throw);
        else if (storedResults.Count > 0) BeginMoveSelectionOrEndTurn();
        else ChangeState(GameFlowState.EndTurn);
    }

    private void EndTurn()
    {
        storedResults.Clear();
        currentTeam = currentTeam == 0 ? 1 : 0;
        RefreshStoredResultText();
        ChangeState(GameFlowState.Throw);
    }

    private bool HasAnyStoredMove()
    {
        foreach (YutResult result in storedResults)
            foreach (PieceView view in pieceViews)
                if (view.Team == currentTeam && !view.Piece.IsFinished &&
                    state.GetMovePaths(view.Piece, result.Steps()).Count > 0) return true;
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
        foreach (GameObject label in resultLabels) if (label != null) Destroy(label);
        resultLabels.Clear();
        if (pathView != null) pathView.ClearHighlights();
    }

    private void RefreshTurnHighlights(bool enabled = true)
    {
        foreach (PieceView view in pieceViews)
            view.SetTurnHighlighted(enabled && view.Team == currentTeam && view.Piece != null && !view.Piece.IsFinished);
    }

    private void RefreshStoredResultText()
    {
        if (yutResultText == null) return;
        if (storedResults.Count == 0) { yutResultText.text = "Stored Results: -"; return; }
        var labels = new List<string>();
        foreach (YutResult result in storedResults) labels.Add($"{GetResultLabel(result)}({result.Steps()})");
        yutResultText.text = $"Stored: {string.Join("  ", labels)}";
    }

    private static TMP_Text FindText(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Color GetResultColor(YutResult result) => result switch
    {
        YutResult.BackDo => new Color(1f, 0.25f, 1f),
        YutResult.Do => Color.yellow,
        YutResult.Gae => new Color(1f, 0.55f, 0.1f),
        YutResult.Geol => Color.red,
        YutResult.Yut => Color.green,
        YutResult.Mo => Color.cyan,
        _ => Color.white
    };

    private static string GetResultLabel(YutResult result) => result switch
    {
        YutResult.BackDo => "빽도",
        YutResult.Do => "도",
        YutResult.Gae => "개",
        YutResult.Geol => "걸",
        YutResult.Yut => "윷",
        YutResult.Mo => "모",
        _ => "-"
    };
}
