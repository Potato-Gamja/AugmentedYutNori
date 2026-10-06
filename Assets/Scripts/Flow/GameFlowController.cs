using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum GameFlowState { GameStart, AugmentSelect, Throw, Move, EndTurn, Finish }

/// <summary>
/// Core 규칙과 화면 구성 요소를 연결하는 게임 흐름 진입점이다.
/// 선택 표시는 GameFlowSelectionController, HUD 데이터 가공은 GameFlowHudPresenter가 담당한다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new();
    [SerializeField] private PathView pathView;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private TextAsset augmentsJson;
    [SerializeField] private bool skipAugmentSelection = true;
    [SerializeField] private GameSetup setup = new GameSetup();

    private readonly Dictionary<Piece, PieceView> views = new();

    private BoardState boardState;
    private GameState gameState;
    private TurnManager turnManager;
    private GameEvents gameEvents;
    private GameRules gameRules;
    private GameHudPanel hud;
    private GameFlowHudPresenter hudPresenter;
    private GameFlowSelectionController selectionController;
    private Camera mainCamera;
    private AugmentCatalog augmentCatalog;
    private GameFlowState currentState;
    private bool isMoving;
    private int teamCount;
    private int augmentSelectionTeam;
    private int? pendingTurnTeam;
    private int? pendingWinnerTeam;
    private string lastStateHash;

    public GameFlowState CurrentState => currentState;
    public int CurrentTeam => turnManager != null ? turnManager.CurrentTeam : 0;
    public IReadOnlyList<YutResult> StoredResults =>
        gameState != null ? gameState.StoredResults : Array.Empty<YutResult>();
    public GameSetup Setup => setup;
    public string LastStateHash => lastStateHash;

    private void Start()
    {
        ResolveSceneReferences();
        if (!ValidateSetup()) return;
        if (boardView == null || boardView.Graph == null)
        {
            Debug.LogError("[GameFlow] BoardView 또는 BoardGraph가 준비되지 않았습니다.", this);
            enabled = false;
            return;
        }
        if (!boardView.ValidateForGameStart(setup.boardLayoutId, out string boardError))
        {
            Debug.LogError($"[GameFlow] {boardError}", this);
            enabled = false;
            return;
        }
        if (augmentsJson == null)
        {
            Debug.LogError($"[GameFlow] AugmentsJson이 준비되지 않았습니다.", this);
            enabled = false;
            return;
        }

        OpenHud();
        CreateCoreState();
        var errors = setup.CheckScenePieces(pieceViews.Select(v => (v.Team, v.SlotIndex)));
        if (errors.Count > 0)
            Debug.LogWarning($"[GameFlow] GameSetup과 씬의 말 구성이 다릅니다. 기존 목록 ID로 실행합니다.\n{string.Join("\n", errors)}", this);
        BindPieces();
        lastStateHash = StateHasher.HashHex(gameState, turnManager);

        hudPresenter = new GameFlowHudPresenter(hud, boardState, gameState);
        selectionController = new GameFlowSelectionController(
            boardView,
            boardState,
            gameRules,
            hud,
            pathView,
            pieceViews,
            () => CurrentTeam,
            MoveUsingOption,
            hudPresenter);

        SubscribeEvents();
        RefreshPieceLayout();
        hudPresenter.RefreshPlayerPanels();
        ChangeState(GameFlowState.GameStart);
    }

    /// <summary>
    /// GAME_START를 받은 뒤 Start 전에 원격 설정을 주입할 때 사용한다.
    /// </summary>
    public void Configure(GameSetup gameSetup)
    {
        if (gameState != null) throw new InvalidOperationException("게임이 시작된 뒤에는 GameSetup을 바꿀 수 없습니다.");
        setup = gameSetup ?? throw new ArgumentNullException(nameof(gameSetup));
    }

    private bool ValidateSetup()
    {
        setup ??= GameSetup.CreateLocalDefault();
        try
        {
            setup.Validate();
            return true;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError($"[GameFlow] GameSetup이 잘못됐습니다. {exception.Message}", this);
            enabled = false;
            return false;
        }
    }

    private void ResolveSceneReferences()
    {
        mainCamera = Camera.main;
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (pathView == null) pathView = FindFirstObjectByType<PathView>();
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();

        foreach (PieceView view in FindObjectsByType<PieceView>(FindObjectsSortMode.None))
            if (!pieceViews.Contains(view)) pieceViews.Add(view);
    }

    private void OpenHud()
    {
        hud = uiManager != null ? uiManager.Open<GameHudPanel>() : null;
        if (hud == null) return;

        hud.ThrowClicked += OnThrowButtonClicked;
        hud.RethrowClicked += OnRethrowButtonClicked;
        hud.WaitingPieceClicked += OnWaitingPieceClicked;
        hud.SetRethrowVisible(false);
    }
    
    private void CreateCoreState()
    {
        teamCount = setup.teamCount;
        boardView.Graph.AllowSkipShortcut = setup.allowSkipShortcut;
        boardState = new BoardState(boardView.Graph);
        gameState = new GameState(boardState);
        turnManager = new TurnManager(gameState, teamCount);
        gameEvents = new GameEvents();
        gameRules = new GameRules(
            gameState,
            turnManager,
            gameEvents,
            new YutThrower(new SystemRandomProvider()),
            new StandardGetResult(),
            CreateAugmentPool());
        /*
        setup.Validate();
        teamCount = setup.teamCount;
        boardView.Graph.AllowSkipShortcut = setup.allowSkipShortcut;
        */
    }

    // json에 저장되어 있는 모든 증강 가져오기
    private AugmentPool CreateAugmentPool()
    {
        AugmentDefinitionFile file = JsonUtility.FromJson<AugmentDefinitionFile>(augmentsJson.text);
        augmentCatalog = new AugmentCatalog(file, AugmentFactories.Create());
        AugmentPool pool = new AugmentPool(new SystemRandomProvider());
        augmentCatalog.RegisterTo(pool);
        return pool;
    }

    private void BindPieces()
    {
        pieceViews.Sort((a, b) =>
        {
            if (a.Team != b.Team) return a.Team.CompareTo(b.Team);
            if (a.SlotIndex >= 0 && b.SlotIndex >= 0 && a.SlotIndex != b.SlotIndex)
                return a.SlotIndex.CompareTo(b.SlotIndex);
            return string.CompareOrdinal(a.name, b.name);
        });

        bool useConfiguredIds = TryValidateConfiguredPieceIds();

        for (int i = 0; i < pieceViews.Count; i++)
        {
            int pieceId = useConfiguredIds && pieceViews[i].TryGetConfiguredPieceId(out int configuredId)
                ? configuredId
                : i;
            Piece piece = boardState.AddPiece(pieceId, pieceViews[i].Team);
            views.Add(piece, pieceViews[i]);
            pieceViews[i].Bind(piece);
            pieceViews[i].SetSortingOrder(10);
        }
    }

    private void SubscribeEvents()
    {
        gameEvents.Thrown += OnThrown;
        gameEvents.PieceMoved += OnMoved;
        gameEvents.TurnChanged += OnTurnChanged;
        gameEvents.AugmentSelected += OnAugmentSelectedEvent;
        gameEvents.GameEnded += OnGameEnded;
        boardState.PiecesReturnedByBoardChange += OnPiecesReturnedByBoardChange;
        boardState.DesyncDetected += OnDesyncDetected;
        boardView.LayoutReloaded += OnBoardLayoutReloaded;
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

        if (boardState != null)
        {
            boardState.PiecesReturnedByBoardChange -= OnPiecesReturnedByBoardChange;
            boardState.DesyncDetected -= OnDesyncDetected;
        }
        if (boardView != null) boardView.LayoutReloaded -= OnBoardLayoutReloaded;
    }

    private void Update()
    {
        if (currentState != GameFlowState.Move || isMoving || !setup.CanControl(CurrentTeam) ||
            Mouse.current == null || mainCamera == null ||
            !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = mainCamera.ScreenToWorldPoint(
            new Vector3(screen.x, screen.y, -mainCamera.transform.position.z));
        world.z = 0f;
        selectionController.HandleClick(world);
    }

    public void OnThrowButtonClicked()
    {
        if (currentState != GameFlowState.Throw || gameRules == null || !setup.CanControl(CurrentTeam)) return;
        // 규격 확인 필요: IGameSession이 준비되면 gameRules 직접 호출 대신 RequestThrow를 호출하고 응답까지 HUD 입력을 잠근다.
        try
        {
            gameRules.ThrowYut();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning($"[GameFlow] {exception.Message}", this);
        }
    }

    public void OnRethrowButtonClicked()
    {
        if (currentState != GameFlowState.Move || gameRules == null || !setup.CanControl(CurrentTeam)) return;
        // 규격 확인 필요: IGameSession.RequestRethrow의 성공/실패 응답 형식에 맞춰 교체한다.
        if (!gameRules.TryRethrow(out _)) return;
        hudPresenter.RefreshStoredResults();
        hudPresenter.RefreshPlayerPanels();
    }

    public void OnAugmentSelected(string augmentId)
    {
        if (currentState != GameFlowState.AugmentSelect || gameRules == null ||
            !setup.CanControl(augmentSelectionTeam)) return;
        // 규격 확인 필요: 서버가 준 AUGMENT_CHOICES 중 하나인지 IGameSession에서 검증한 뒤 결과 이벤트로 갱신한다.
        gameRules.SelectAugment(augmentSelectionTeam, augmentId);

        augmentSelectionTeam++;
        if (augmentSelectionTeam < teamCount)
        {
            ChangeState(GameFlowState.AugmentSelect);
            return;
        }

        augmentSelectionTeam = 0;
        ChangeState(GameFlowState.Throw);
    }

    private void OnThrown(YutThrowOutcome outcome)
    {
        hud?.ShowThrowResult(outcome.Result, gameState.RemainingThrows, CurrentTeam);
        hudPresenter.RefreshStoredResults();

        if (gameState.RemainingThrows > 0)
        {
            ChangeState(GameFlowState.Throw);
            hud?.SetInstruction($"{GameFlowHudPresenter.ResultLabel(outcome.Result)}! Throw again.");
        }
        else if (gameRules.HasAnyStoredMove())
        {
            ChangeState(GameFlowState.Move);
        }
    }

    private void OnAugmentSelectedEvent(IAugment augment) => hudPresenter.RefreshPlayerPanels();

    private void OnTurnChanged(int team)
    {
        hudPresenter.RefreshStoredResults();
        hudPresenter.RefreshPlayerPanels();
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
        if (next != GameFlowState.AugmentSelect) uiManager?.Close<AugmentSelectPanel>();
        currentState = next;

        switch (next)
        {
            case GameFlowState.GameStart:
                augmentSelectionTeam = 0;
                ChangeState(skipAugmentSelection ? GameFlowState.Throw : GameFlowState.AugmentSelect);
                break;

            case GameFlowState.Throw:
                PrepareInteractiveState();
                hud?.SetInteractionState(setup.CanControl(CurrentTeam), false);
                hud?.SetTurn($"Team {CurrentTeam + 1} Turn", CurrentTeam);
                hud?.SetInstruction(gameState.StoredResults.Count > 0 ? "Bonus throw." : "Throw Yut.");
                hud?.SetThrowInteractable(gameState.RemainingThrows > 0);
                hud?.SetRethrowVisible(false);
                hud?.SetTimerRunning(true);
                break;

            case GameFlowState.Move:
                PrepareInteractiveState();
                hud?.SetInteractionState(setup.CanControl(CurrentTeam), false);
                hud?.SetTurn($"Team {CurrentTeam + 1} Turn", CurrentTeam);
                hud?.SetInstruction("Select a piece.");
                hud?.SetThrowInteractable(false);
                hud?.SetRethrowVisible(hudPresenter.CanUseRethrow(CurrentTeam));
                hud?.SetTimerRunning(true);
                break;

            case GameFlowState.Finish:
                selectionController.Clear();
                selectionController.RefreshTurnHighlights(false);
                hudPresenter.RefreshPlayerPanels();
                hud?.SetInteractionState(false, false);
                int winner = pendingWinnerTeam ?? gameState.WinnerTeam ?? CurrentTeam;
                hud?.SetInstruction($"Team {winner + 1} wins!");
                hud?.SetThrowInteractable(false);
                hud?.SetRethrowVisible(false);
                hud?.SetTimerRunning(false);
                uiManager?.Open<ResultPopup>()?.SetWinner(
                    $"Team {winner + 1}",
                    GameHudPanel.GetTeamColor(winner));
                break;

            case GameFlowState.AugmentSelect:
                OpenAugmentSelection();
                break;
        }
    }

    private void PrepareInteractiveState()
    {
        selectionController.Clear();
        selectionController.RefreshTurnHighlights();
        hudPresenter.RefreshPlayerPanels();
    }

    private void OpenAugmentSelection()
    {
        selectionController.Clear();
        AugmentSelectPanel panel = uiManager != null ? uiManager.Get<AugmentSelectPanel>() : null;
        if (panel == null)
        {
            Debug.LogError("[GameFlow] AugmentSelectPanel이 UIManager에 등록되지 않았습니다.", this);
            return;
        }

        hud?.SetTurn($"Team {augmentSelectionTeam + 1} Augment", augmentSelectionTeam);
        hud?.SetThrowInteractable(false);
        hud?.SetRethrowVisible(false);
        hud?.SetTimerRunning(false);

        if (!setup.CanControl(augmentSelectionTeam))
        {
            uiManager?.Close<AugmentSelectPanel>();
            hud?.SetInstruction("Waiting for augment selection.");
            return;
        }

        // 원격 모드는 서버의 AUGMENT_CHOICES를 GameRules.SetAugmentChoices로 먼저 적용한 뒤
        // IGameSession 이벤트에서 이 화면을 열어야 한다. 현재는 로컬 모드 후보만 생성한다.
        if (!setup.IsLocalMode)
        {
            hud?.SetInstruction("Waiting for server augment choices.");
            return;
        }

        List<AugmentDefinition> selection = new();
        foreach (var id in gameRules.GetAugmentChoices(augmentSelectionTeam, 3))
            selection.Add(augmentCatalog.Find(id));
        panel.Setup(selection, OnAugmentSelected);
        uiManager.Open<AugmentSelectPanel>();
        hud?.SetInstruction("Select an augment.");
    }

    private void OnWaitingPieceClicked(int team, int slotIndex)
    {
        if (currentState != GameFlowState.Move || isMoving || team != CurrentTeam ||
            !setup.CanControl(team)) return;
        selectionController.HandleWaitingPieceClick(team, slotIndex);
    }

    private void MoveUsingOption(MoveOption option)
    {
        PieceView movingView = selectionController.SelectedPiece;
        if (movingView == null) return;

        hud?.MarkResultPending(option.ResultIndex, CurrentTeam);
        bool entersBoard = movingView.Piece.IsWaiting;
        selectionController.Clear();
        if (entersBoard) movingView.SetWorldVisible(true);

        // 규격 확인 필요: IGameSession.RequestMove(pieceId, resultIndex, destinationNodeId)로 교체하고
        // MOVE_APPLIED를 받은 뒤에만 실제 이동 애니메이션을 재생한다.
        if (gameRules.MovePiece(movingView.Piece.Id, option.ResultIndex, option.DestinationNodeId)) return;

        if (entersBoard) movingView.SetWorldVisible(false);
        hudPresenter.RefreshStoredResults();
        ChangeState(gameState.RemainingThrows > 0 ? GameFlowState.Throw : GameFlowState.Move);
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
        hudPresenter.RefreshPlayerPanels();
        hudPresenter.RefreshStoredResults();
        isMoving = false;
        lastStateHash = StateHasher.HashHex(gameState, turnManager);

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

    private void OnPiecesReturnedByBoardChange(IReadOnlyList<Piece> returned)
    {
        foreach (Piece piece in returned)
            if (views.TryGetValue(piece, out PieceView view))
                view.ReturnHome();
    }

    private void OnDesyncDetected(MoveCommand command)
    {
        Debug.LogError("[GameFlow] 서버 이동을 현재 상태에서 재현할 수 없습니다. 전체 상태 동기화가 필요합니다.", this);
        selectionController?.Clear();
        hud?.SetInteractionState(false, false);
        hud?.SetInstruction("State sync required.");
    }

    private void OnBoardLayoutReloaded()
    {
        selectionController.Clear();
        RefreshPieceLayout();
        hudPresenter.RefreshPlayerPanels();
        selectionController.RefreshTurnHighlights(currentState != GameFlowState.Finish);
        if (currentState == GameFlowState.Move) hud?.SetInstruction("Select a piece.");
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
                float x = teams.Count == 1 ? 0f : (i - (teams.Count - 1) * 0.5f) * 0.55f;
                Vector3 position = boardView.GetView(node).Position + new Vector3(x, 0f, 0f);
                foreach (Piece piece in pieces)
                    if (piece.Team == teams[i]) views[piece].SetBoardPosition(position);
            }
        }
    }

    private bool TryValidateConfiguredPieceIds()
    {
        var ids = new HashSet<int>();
        foreach (PieceView view in pieceViews)
        {
            if (view == null || !view.TryGetConfiguredPieceId(out int id) || !ids.Add(id))
            {
                Debug.LogWarning(
                    "[GameFlow] PieceView의 Team/SlotIndex가 모두 고유하게 설정되지 않아 기존 목록 순서 ID를 사용합니다. " +
                    "멀티플레이 연결 전 각 팀의 SlotIndex를 0부터 지정해 주세요.", this);
                return false;
            }
        }
        return true;
    }
}
