using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum GameFlowState { GameStart, AugmentSelect, Throw, Move, EndTurn, Finish }

/// <summary>
/// Core의 GameRules를 화면과 입력에 연결하는 Controller.
/// 규칙 계산은 GameRules/GameState/TurnManager가 담당하고,
/// 이 클래스는 상태 화면 전환, 말 선택, 이동 애니메이션과 HUD 갱신만 담당한다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new();
    [SerializeField] private PathView pathView;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private bool skipAugmentSelection = true;
    [SerializeField] private bool allowSkipShortcut;

    private readonly Dictionary<Piece, PieceView> views = new();
    private readonly Dictionary<NodeView, List<MoveOption>> targets = new();
    private readonly HashSet<NodeView> highlighted = new();
    private readonly List<GameObject> labels = new();

    private BoardState boardState;
    private GameState gameState;
    private TurnManager turnManager;
    private GameEvents gameEvents;
    private GameRules gameRules;

    private GameHudPanel hud;
    private PieceView selectedPiece;
    private Camera mainCamera;
    private GameFlowState currentState;
    private bool isMoving;
    private int augmentSelectionTeam;
    private int? pendingTurnTeam;
    private int? pendingWinnerTeam;

    public GameFlowState CurrentState => currentState;
    public int CurrentTeam => turnManager != null ? turnManager.CurrentTeam : 0;
    public IReadOnlyList<YutResult> StoredResults =>
        gameState != null ? gameState.StoredResults : Array.Empty<YutResult>();

    private void Start()
    {
        mainCamera = Camera.main;
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (pathView == null) pathView = FindFirstObjectByType<PathView>();
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();

        foreach (PieceView view in FindObjectsByType<PieceView>(FindObjectsSortMode.None))
            if (!pieceViews.Contains(view)) pieceViews.Add(view);

        if (boardView == null || boardView.Graph == null)
        {
            Debug.LogError("[GameFlow] BoardView 또는 BoardGraph가 준비되지 않았습니다.", this);
            enabled = false;
            return;
        }

        hud = uiManager != null ? uiManager.Open<GameHudPanel>() : null;
        if (hud != null)
        {
            hud.ThrowClicked += OnThrowButtonClicked;
            hud.RethrowClicked += OnRethrowButtonClicked;
            hud.WaitingPieceClicked += OnWaitingPieceClicked;
            hud.SetRethrowVisible(false);
        }

        boardView.Graph.AllowSkipShortcut = allowSkipShortcut;
        boardState = new BoardState(boardView.Graph);

        pieceViews.Sort((a, b) =>
            a.Team != b.Team ? a.Team.CompareTo(b.Team) : string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < pieceViews.Count; i++)
        {
            Piece piece = boardState.AddPiece(i, pieceViews[i].Team);
            views.Add(piece, pieceViews[i]);
            pieceViews[i].Bind(piece);
            pieceViews[i].SetSortingOrder(10);
        }

        gameState = new GameState(boardState);
        turnManager = new TurnManager(gameState, 2);
        gameEvents = new GameEvents();
        gameRules = new GameRules(
            gameState,
            turnManager,
            gameEvents,
            new YutThrower(new SystemRandomProvider()),
            new StandardGetResult());

        gameEvents.Thrown += OnThrown;
        gameEvents.PieceMoved += OnMoved;
        gameEvents.TurnChanged += OnTurnChanged;
        gameEvents.AugmentSelected += OnAugmentSelectedEvent;
        gameEvents.GameEnded += OnGameEnded;

        // 실행 중 판이 바뀌면(BoardView.SwitchLayout) 잡혔을 때처럼 화면에 바로 반영
        boardState.PiecesReturnedByBoardChange += OnPiecesReturnedByBoardChange;
        boardView.LayoutReloaded += OnBoardLayoutReloaded;

        RefreshPieceLayout();
        RefreshPlayerPanels();
        ChangeState(GameFlowState.GameStart);
    }

    private void OnDestroy()
    {
        if (hud != null)
        {
            hud.ThrowClicked -= OnThrowButtonClicked;
            hud.RethrowClicked -= OnRethrowButtonClicked;
            hud.WaitingPieceClicked -= OnWaitingPieceClicked;
        }

        if (gameEvents != null)
        {
            gameEvents.Thrown -= OnThrown;
            gameEvents.PieceMoved -= OnMoved;
            gameEvents.TurnChanged -= OnTurnChanged;
            gameEvents.AugmentSelected -= OnAugmentSelectedEvent;
            gameEvents.GameEnded -= OnGameEnded;
        }

        if (boardState != null) boardState.PiecesReturnedByBoardChange -= OnPiecesReturnedByBoardChange;
        if (boardView != null) boardView.LayoutReloaded -= OnBoardLayoutReloaded;
    }

    // 판이 바뀌어 서 있던 칸이 없어진 말: 잡혔을 때처럼 대기석으로.
    // 판 데이터가 바뀌는 도중에 오므로 말 오브젝트만 다루고, 화면 갱신은 OnBoardLayoutReloaded에서 한다
    private void OnPiecesReturnedByBoardChange(IReadOnlyList<Piece> returned)
    {
        foreach (Piece piece in returned)
            if (views.TryGetValue(piece, out PieceView view))
                view.ReturnHome();
    }

    // 판 배치가 끝난 뒤: 옛 판 기준 선택/후보를 지우고 말 위치와 플레이어 패널을 다시 그린다
    private void OnBoardLayoutReloaded()
    {
        ClearSelection();
        RefreshPieceLayout();
        RefreshPlayerPanels();
        RefreshTurnHighlights(currentState != GameFlowState.Finish);
        if (currentState == GameFlowState.Move) hud?.SetInstruction("Select a piece.");
    }

    private void Update()
    {
        if (currentState != GameFlowState.Move || isMoving || Mouse.current == null || mainCamera == null ||
            !Mouse.current.leftButton.wasPressedThisFrame) return;

        // PlayerPanel 버튼을 누른 프레임에는 같은 클릭을 월드 보드 입력으로 다시 처리하지 않는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = mainCamera.ScreenToWorldPoint(
            new Vector3(screen.x, screen.y, -mainCamera.transform.position.z));
        world.z = 0f;
        HandleClick(world);
    }

    public void OnThrowButtonClicked()
    {
        if (currentState != GameFlowState.Throw || gameRules == null) return;

        try
        {
            gameRules.ThrowYut();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning($"[GameFlow] {exception.Message}", this);
        }
    }

    private void OnThrown(YutThrowOutcome outcome)
    {
        hud?.ShowThrowResult(outcome.Result, gameState.RemainingThrows, CurrentTeam);
        RefreshStoredResultText();

        if (gameState.RemainingThrows > 0)
        {
            ChangeState(GameFlowState.Throw);
            hud?.SetInstruction($"{ResultLabel(outcome.Result)}! Throw again.");
        }
        else if (gameRules.HasAnyStoredMove())
        {
            ChangeState(GameFlowState.Move);
        }
        // 이동할 수 없는 결과라면 GameRules가 이어서 TurnChanged를 발생시킨다.
    }

    public void OnRethrowButtonClicked()
    {
        if (currentState != GameFlowState.Move || gameRules == null) return;
        if (gameRules.TryRethrow(out _))
        {
            RefreshStoredResultText();
            RefreshPlayerPanels();
        }
    }

    public void OnAugmentSelected(string augmentId)
    {
        if (currentState != GameFlowState.AugmentSelect || gameRules == null) return;

        gameRules.SelectAugment(augmentSelectionTeam, augmentId);

        if (augmentSelectionTeam == 0)
        {
            augmentSelectionTeam = 1;
            ChangeState(GameFlowState.AugmentSelect);
            return;
        }

        augmentSelectionTeam = 0;
        ChangeState(GameFlowState.Throw);
    }

    private void OnAugmentSelectedEvent(IAugment augment)
    {
        RefreshPlayerPanels();
    }

    private void OnTurnChanged(int team)
    {
        RefreshStoredResultText();
        RefreshPlayerPanels();

        if (isMoving)
        {
            pendingTurnTeam = team;
            return;
        }

        ChangeState(GameFlowState.Throw);
    }

    private void OnGameEnded(int winnerTeam)
    {
        pendingWinnerTeam = winnerTeam;
        if (!isMoving) ChangeState(GameFlowState.Finish);
    }
    private void ChangeState(GameFlowState next)
    {
        // 증강 선택을 마치고 다른 단계로 넘어가면 Popup 레이어를 즉시 닫는다.
        if (next != GameFlowState.AugmentSelect) uiManager?.Close<AugmentSelectPanel>();
        currentState = next;

        if (next == GameFlowState.GameStart)
        {
            augmentSelectionTeam = 0;
            ChangeState(skipAugmentSelection ? GameFlowState.Throw : GameFlowState.AugmentSelect);
        }
        else if (next == GameFlowState.Throw)
        {
            ClearSelection();
            RefreshTurnHighlights();
            RefreshPlayerPanels();
            hud?.SetTurn($"Team {CurrentTeam + 1} Turn");
            hud?.SetInstruction(gameState.StoredResults.Count > 0 ? "Bonus throw." : "Throw Yut.");
            hud?.SetThrowInteractable(gameState.RemainingThrows > 0);
            hud?.SetRethrowVisible(false);
            hud?.SetTimerRunning(true);
        }
        else if (next == GameFlowState.Move)
        {
            ClearSelection();
            RefreshTurnHighlights();
            RefreshPlayerPanels();
            hud?.SetTurn($"Team {CurrentTeam + 1} Turn");
            hud?.SetInstruction("Select a piece.");
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(CanUseRethrow());
            hud?.SetTimerRunning(true);
        }
        else if (next == GameFlowState.Finish)
        {
            ClearSelection();
            RefreshTurnHighlights(false);
            RefreshPlayerPanels();
            int winner = pendingWinnerTeam ?? gameState.WinnerTeam ?? CurrentTeam;
            hud?.SetInstruction($"Team {winner + 1} wins!");
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(false);
            hud?.SetTimerRunning(false);
            uiManager?.Open<ResultPopup>()?.SetWinner(
                $"Team {winner + 1}",
                winner == 0 ? Color.red : Color.blue);
        }
        else if (next == GameFlowState.AugmentSelect)
        {
            ClearSelection();
            AugmentSelectPanel panel = uiManager != null ? uiManager.Get<AugmentSelectPanel>() : null;
            if (panel == null)
            {
                Debug.LogError("[GameFlow] AugmentSelectPanel이 UIManager에 등록되지 않았습니다.", this);
                return;
            }

            panel.Setup(gameRules.GetAugmentChoices(augmentSelectionTeam, 3), OnAugmentSelected);
            uiManager.Open<AugmentSelectPanel>();
            
            hud?.SetTurn($"Team {augmentSelectionTeam + 1} Augment");
            hud?.SetInstruction("Select an augment.");
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(false);
            hud?.SetTimerRunning(false);
        }
    }

    private void HandleClick(Vector3 world)
    {
        NodeView node = PickNode(world);
        PieceView clicked = PickPiece(world);

        if (selectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !targets.ContainsKey(target)) &&
                clicked != null && clicked.Piece.IsOnBoard)
                target = boardView.GetView(clicked.Piece.Node);

            if (target != null &&
                targets.TryGetValue(target, out List<MoveOption> options) &&
                options.Count > 0)
            {
                // 모+모처럼 결과와 증강 적용값이 완전히 같은 선택지는 어느 것을
                // 소비해도 이후 상태가 같으므로 별도 선택창 없이 첫 결과를 사용한다.
                // 도(+1)와 개처럼 서로 다른 결과가 증강 때문에 같은 칸에 도착한
                // 경우에는 아래 선택창을 열어 플레이어가 소비할 결과를 고르게 한다.
                bool equivalentOptions = AreEquivalentMoveOptions(options);
                if (options.Count == 1 || equivalentOptions)
                {
                    MoveUsingOption(options[0]);
                }
                else
                {
                    // 서로 다른 윷 결과가 같은 칸에 도착하면 자동으로 첫 결과를 쓰지 않고
                    // 플레이어가 소비할 결과를 직접 고르게 한다.
                    var choices = new List<MoveOption>(options);
                    var labels = new List<string>(choices.Count);
                    foreach (MoveOption option in choices) labels.Add(MoveOptionLabel(option));
                    hud?.ShowMoveOptionChoices(labels, CurrentTeam, index =>
                    {
                        if (index >= 0 && index < choices.Count) MoveUsingOption(choices[index]);
                    });
                }
                return;
            }
        }

        if (clicked != null && clicked.Team == CurrentTeam && !clicked.Piece.IsFinished)
            SelectPiece(clicked == selectedPiece ? null : clicked);
    }

    private static bool AreEquivalentMoveOptions(IReadOnlyList<MoveOption> options)
    {
        if (options == null || options.Count < 2) return true;

        MoveOption first = options[0];
        for (int i = 1; i < options.Count; i++)
        {
            MoveOption other = options[i];
            if (other.Result != first.Result ||
                other.ModifiedMoveCount != first.ModifiedMoveCount ||
                other.DestinationNodeId != first.DestinationNodeId ||
                other.IsFinished != first.IsFinished)
                return false;
        }
        return true;
    }

    private void OnWaitingPieceClicked(int team, int slotIndex)
    {
        if (currentState != GameFlowState.Move || isMoving || team != CurrentTeam) return;

        int waitingIndex = 0;
        foreach (PieceView view in pieceViews)
        {
            if (view.Team != team || view.Piece == null || !view.Piece.IsWaiting) continue;
            if (waitingIndex == slotIndex)
            {
                SelectPiece(view == selectedPiece ? null : view);
                return;
            }
            waitingIndex++;
        }
    }

    private void SelectPiece(PieceView piece)
    {
        ClearSelection();
        selectedPiece = piece;
        if (piece == null) return;

        piece.SetSelected(true);
        if (piece.Piece.IsWaiting)
            hud?.SetSelectedWaitingPiece(piece.Team, GetWaitingSlotIndex(piece));

        hud?.SetInstruction("Select a destination.");
        RefreshStoredResultText(piece.Piece);

        foreach (MoveOption option in gameRules.GetMoveOptions(piece.Piece.Id))
        {
            BoardNode destinationNode = boardState.Graph.GetNode(option.DestinationNodeId);
            NodeView destination = boardView.GetView(destinationNode);
            if (destination == null) continue;

            if (!targets.TryGetValue(destination, out List<MoveOption> options))
            {
                options = new List<MoveOption>();
                targets.Add(destination, options);
            }
            options.Add(option);
        }

        foreach (KeyValuePair<NodeView, List<MoveOption>> pair in targets)
        {
            Color color = pair.Value.Count == 1 ? ResultColor(pair.Value[0].Result) : Color.white;
            
            pair.Key.SetDestinationBadge(GetDestinationBadge(pair.Key, pair.Value, piece.Team), piece.Team);
pair.Key.SetHighlight(color);
            highlighted.Add(pair.Key);
            CreateLabel(pair.Key, pair.Value);
        }
    }

    private int GetWaitingSlotIndex(PieceView target)
    {
        int index = 0;
        foreach (PieceView view in pieceViews)
        {
            if (view.Team != target.Team || view.Piece == null || !view.Piece.IsWaiting) continue;
            if (view == target) return index;
            index++;
        }
        return -1;
    }

    private void MoveUsingOption(MoveOption option)
    {
        PieceView movingView = selectedPiece;
        if (movingView == null) return;

        
        hud?.MarkResultPending(option.ResultIndex, CurrentTeam);
bool entersBoard = movingView.Piece.IsWaiting;
        ClearSelection();
        if (entersBoard) movingView.SetWorldVisible(true);

        if (!gameRules.MovePiece(
                movingView.Piece.Id,
                option.ResultIndex,
                option.DestinationNodeId))
        {
            if (entersBoard) movingView.SetWorldVisible(false);
            RefreshStoredResultText();
            ChangeState(gameState.RemainingThrows > 0
                ? GameFlowState.Throw
                : GameFlowState.Move);
        }
    }

    private void CreateLabel(NodeView destination, IReadOnlyList<MoveOption> options)
    {
        var go = new GameObject("MoveResultLabel");
        go.transform.SetParent(destination.transform, false);
        go.transform.localPosition = new Vector3(0f, 1.02f, 0f);

        TextMesh text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 54;
        text.characterSize = 0.055f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;

        var names = new List<string>();
        foreach (MoveOption option in options)
        {
            string name = MoveOptionLabel(option);
            if (!names.Contains(name)) names.Add(name);
        }

        text.text = string.Join("/", names);
        text.color = options.Count == 1 ? ResultColor(options[0].Result) : Color.white;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        renderer.sortingOrder = 35;
        labels.Add(go);
    }

    private void OnMoved(MoveResult result)
    {
        var points = new List<Vector3>();
        foreach (BoardNode node in result.Path.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view != null) points.Add(view.Position);
        }

        isMoving = true;
        int remaining = result.MovedPieces.Count;
        foreach (Piece piece in result.MovedPieces)
        {
            PieceView view = views[piece];
            view.SetWorldVisible(true);
            view.SetSortingOrder(20);
            view.PlayMove(points, result.Path.IsFinished, () =>
            {
                if (--remaining == 0) FinishMove(result);
            });
        }
    }

    private void FinishMove(MoveResult result)
    {
        foreach (Piece captured in result.CapturedPieces)
            if (views.TryGetValue(captured, out PieceView capturedView))
                capturedView.ReturnHome();

        RefreshPieceLayout();
        RefreshPlayerPanels();
        RefreshStoredResultText();
        isMoving = false;

        if (pendingWinnerTeam.HasValue || gameState.WinnerTeam.HasValue)
        {
            ChangeState(GameFlowState.Finish);
            return;
        }

        if (pendingTurnTeam.HasValue)
        {
            pendingTurnTeam = null;
            ChangeState(GameFlowState.Throw);
            return;
        }

        if (gameState.RemainingThrows > 0)
            ChangeState(GameFlowState.Throw);
        else if (gameState.StoredResults.Count > 0 && gameRules.HasAnyStoredMove())
            ChangeState(GameFlowState.Move);
    }

    private void RefreshPieceLayout()
    {
        foreach (PieceView view in pieceViews)
        {
            view.SetSortingOrder(10);
            bool onBoard = view.Piece != null && view.Piece.IsOnBoard;
            view.SetWorldVisible(onBoard);
            if (onBoard) view.SetStackCount(boardState.GetStack(view.Piece).Count);
        }

        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            List<Piece> pieces = boardState.GetPiecesAt(node);
            var teams = new List<int>();
            foreach (Piece piece in pieces)
                if (!teams.Contains(piece.Team)) teams.Add(piece.Team);

            teams.Sort();
            for (int i = 0; i < teams.Count; i++)
            {
                float x = teams.Count == 1
                    ? 0f
                    : (i - (teams.Count - 1) * 0.5f) * 0.55f;
                Vector3 position = boardView.GetView(node).Position + new Vector3(x, 0f, 0f);
                foreach (Piece piece in pieces)
                    if (piece.Team == teams[i]) views[piece].SetBoardPosition(position);
            }
        }
    }

    private void RefreshPlayerPanels()
    {
        if (hud == null || boardState == null) return;

        for (int team = 0; team < 2; team++)
        {
            int waiting = 0;
            int onBoard = 0;
            int finished = 0;

            foreach (Piece piece in boardState.Pieces)
            {
                if (piece.Team != team) continue;
                if (piece.IsWaiting) waiting++;
                else if (piece.IsOnBoard) onBoard++;
                else if (piece.IsFinished) finished++;
            }

            var augmentIds = new List<string>();
            foreach (IAugment augment in boardState.GetAugments(team))
                augmentIds.Add(augment.Id);

            hud.SetPlayerState(team, waiting, onBoard, finished, augmentIds);
        }
    }

    private PieceView PickPiece(Vector3 world)
    {
        foreach (PieceView view in pieceViews)
            if (view != null && view.gameObject.activeInHierarchy && view.Contains(world))
                return view;
        return null;
    }

    private NodeView PickNode(Vector3 world)
    {
        NodeView best = null;
        float distanceBest = float.MaxValue;

        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view == null) continue;

            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;
            float distance = Vector2.Distance(world, view.Position);
            if (distance <= radius && distance < distanceBest)
            {
                best = view;
                distanceBest = distance;
            }
        }
        return best;
    }
    private void ClearSelection()
    {
        hud?.HideMoveOptionChoices();
        if (selectedPiece != null) selectedPiece.SetSelected(false);
        selectedPiece = null;
        hud?.SetSelectedWaitingPiece(-1, -1);

        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        targets.Clear();

        foreach (GameObject label in labels)
            if (label != null) Destroy(label);
        labels.Clear();

        if (pathView != null) pathView.ClearHighlights();
    }

    private void RefreshTurnHighlights(bool enabled = true)
    {
        foreach (PieceView view in pieceViews)
            view.SetTurnHighlighted(
                enabled &&
                view.Team == CurrentTeam &&
                view.Piece != null &&
                !view.Piece.IsFinished);
    }

    private void RefreshStoredResultText(Piece contextPiece = null)
    {
        if (hud == null || gameState == null) return;
        if (gameState.StoredResults.Count == 0)
        {
            hud.SetResults("Stored Results: -");
            return;
        }

        var results = new List<string>();
        foreach (YutResult result in gameState.StoredResults)
        {
            int baseCount = result.Steps();
            int modified = contextPiece != null
                ? boardState.GetModifiedMoveCount(contextPiece, baseCount)
                : baseCount;
            int bonus = modified - baseCount;
            string bonusText = bonus != 0 ? $" ({bonus:+0;-0})" : "";
            results.Add($"{ResultLabel(result)} {baseCount}{bonusText}");
        }
        hud.SetResults($"Stored: {string.Join("  ", results)}");
    }

    private RethrowAugment GetRethrowAugment()
    {
        if (boardState == null) return null;
        foreach (IAugment augment in boardState.GetAugments(CurrentTeam))
            if (augment is RethrowAugment rethrow) return rethrow;
        return null;
    }

    private bool CanUseRethrow()
    {
        if (gameState == null || gameState.StoredResults.Count == 0) return false;
        RethrowAugment rethrow = GetRethrowAugment();
        return rethrow != null &&
               !gameState.HasMovedThisTurn &&
               rethrow.CanUse(
                   boardState,
                   CurrentTeam,
                   gameState.StoredResults[gameState.StoredResults.Count - 1]);
    }

    private static string MoveOptionLabel(MoveOption option)
    {
        int baseCount = option.Result.Steps();
        int bonus = option.ModifiedMoveCount - baseCount;
        string bonusText = bonus != 0 ? $" ({bonus:+0;-0})" : "";
        return $"{ResultLabel(option.Result)} {baseCount}{bonusText}";
    }

    private static Color ResultColor(YutResult result) => result switch
    {
        YutResult.BackDo => new Color(1f, 0.25f, 1f),
        YutResult.Do => Color.yellow,
        YutResult.Gae => new Color(1f, 0.55f, 0.1f),
        YutResult.Geol => Color.red,
        YutResult.Yut => Color.green,
        YutResult.Mo => Color.cyan,
        _ => Color.white
    };

    private static string ResultLabel(YutResult result) => result switch
    {
        YutResult.BackDo => "빽도",
        YutResult.Do => "도",
        YutResult.Gae => "개",
        YutResult.Geol => "걸",
        YutResult.Yut => "윷",
        YutResult.Mo => "모",
        _ => "-"
    };


/// <summary>
    /// 목적지에 표시할 의미를 현재 보드 상태에서 계산한다.
    /// 완주를 우선하고, 같은 팀은 업기, 상대 팀은 잡기로 표시한다.
    /// </summary>
    private DestinationBadgeType GetDestinationBadge(
        NodeView destination,
        IReadOnlyList<MoveOption> options,
        int movingTeam)
    {
        foreach (MoveOption option in options)
            if (option.IsFinished) return DestinationBadgeType.Finish;

        BoardNode node = destination.Node ?? boardState.Graph.GetNode(destination.Id);
        if (node == null) return DestinationBadgeType.None;

        bool hasSameTeam = false;
        foreach (Piece occupant in boardState.GetPiecesAt(node))
        {
            if (occupant.Team != movingTeam) return DestinationBadgeType.Capture;
            hasSameTeam = true;
        }

        return hasSameTeam ? DestinationBadgeType.Carry : DestinationBadgeType.None;
    }
}

