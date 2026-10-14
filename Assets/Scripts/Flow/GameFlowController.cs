using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField] private string mainMenuSceneName = "Lobby";

    private readonly Dictionary<Piece, PieceView> views = new();

    private BoardState boardState;
    private GameState gameState;
    private TurnManager turnManager;
    private GameEvents gameEvents;
    private GameSoundBinder soundBinder;
    private GameRules gameRules;
    // Flow는 규칙 구현을 직접 실행하지 않고 세션에 명령을 보낸 뒤 확정 이벤트만 화면에 반영한다.
    private IGameSession gameSession;
    private IGameSessionFactory sessionFactory;
    private GameHudPanel hud;
    private GameFlowHudPresenter hudPresenter;
    private GameFlowSelectionController selectionController;
    private SettingsPanel settingsPanel;
    private ResultPopup resultPopup;
    private Camera mainCamera;
    private AugmentCatalog augmentCatalog;
    private GameFlowState currentState;
    private bool isMoving;
    private bool piecesCreated;
    private bool leavingGame;
    private int teamCount;
    private int augmentSelectionTeam;
    private IReadOnlyList<string> currentAugmentChoices = Array.Empty<string>();
    private bool localAugmentConfirmed;
    private int? pendingTurnTeam;
    private int? pendingWinnerTeam;
    private string lastStateHash;
    private readonly Dictionary<string, int> playerTeams = new();
    private IReadOnlyList<PlayerDisplayInfo> playerProfiles = Array.Empty<PlayerDisplayInfo>();

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
        // 말은 증강이 모두 확정된 뒤 만든다 (노장은 말 수가 바뀜). 그 전까지 씬의 말은 숨긴다
        foreach (PieceView view in pieceViews) view.gameObject.SetActive(false);

        hudPresenter = new GameFlowHudPresenter(
            hud,
            boardState,
            gameState,
            augmentCatalog,
            pieceViews,
            () => CurrentTeam);
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
        BindSettingsPanel();
        hudPresenter.RefreshPlayerPanels();
        ChangeState(GameFlowState.GameStart);
        if (gameSession is RemoteGameSession remoteSession) remoteSession.ReplayPendingMessages();
    }

    /// <summary>
    /// GAME_START를 받은 뒤 Start 전에 원격 설정을 주입할 때 사용한다.
    /// </summary>
    public void Configure(GameSetup gameSetup)
    {
        if (gameState != null) throw new InvalidOperationException("게임이 시작된 뒤에는 GameSetup을 바꿀 수 없습니다.");
        setup = gameSetup ?? throw new ArgumentNullException(nameof(gameSetup));
    }

    /// <summary>
    /// GameInstaller가 게임 설정과 Local/Remote 세션 생성 방식을 함께 주입할 때 사용한다.
    /// Network 담당은 GameFlowController를 직접 수정하지 않고 GameInstaller에 Remote Factory를 등록한다.
    /// </summary>
    public void Configure(GameSetup gameSetup, IGameSessionFactory factory)
    {
        Configure(gameSetup);
        sessionFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public void ConfigurePlayerProfiles(IReadOnlyList<PlayerDisplayInfo> profiles)
    {
        if (gameState != null) throw new InvalidOperationException("게임이 시작된 뒤에는 플레이어 표시 정보를 바꿀 수 없습니다.");
        // GameInstaller가 전달 후 임시 목록을 비워도 표시 정보는 유지한다.
        playerProfiles = profiles?.Select(profile => profile == null ? null : new PlayerDisplayInfo
        {
            playerId = profile.playerId,
            nickname = profile.nickname,
            team = profile.team,
            iconId = profile.iconId,
        }).ToArray() ?? Array.Empty<PlayerDisplayInfo>();
        playerTeams.Clear();
        foreach (PlayerDisplayInfo profile in playerProfiles)
            if (profile != null) RegisterPlayerTeam(profile.playerId, profile.team);
    }

    private bool ValidateSetup()
    {
        setup ??= GameSetup.CreateLocalDefault();
        try
        {
            setup.Validate();
            if (setup.teamCount > 4)
                throw new ArgumentException($"클라이언트는 2~4팀만 지원합니다. (현재 {setup.teamCount})");
            if (setup.piecesPerTeam > 4)
                throw new ArgumentException($"현재 HUD는 팀당 1~4개의 말만 지원합니다. (현재 {setup.piecesPerTeam})");
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
        hud.SettingsClicked += OnSettingsClicked;
        hud.ConfigurePlayers(setup.teamCount, setup.piecesPerTeam);
        hud.ConfigurePlayerProfiles(playerProfiles);
        hud.SetRethrowVisible(false);
    }

    private void EnsurePieceViewsForSetup()
    {
        pieceViews.RemoveAll(view => view == null);

        foreach (PieceView view in pieceViews)
        {
            bool used = view.Team >= 0 && view.Team < setup.teamCount &&
                        view.SlotIndex >= 0 && view.SlotIndex < PieceCount(view.Team);
            view.gameObject.SetActive(used);
        }

        for (int team = 0; team < setup.teamCount; team++)
        {
            int count = PieceCount(team);
            for (int slot = 0; slot < count; slot++)
            {
                if (pieceViews.Exists(view => view.Team == team && view.SlotIndex == slot)) continue;

                PieceView source = pieceViews.Find(view => view.Team == 0 && view.SlotIndex == slot);
                if (source == null) source = pieceViews.Find(view => view.SlotIndex == slot);
                if (source == null)
                {
                    Debug.LogError($"[GameFlow] 팀 {team + 1}, 말 {slot + 1}을 만들 기준 PieceView가 없습니다.", this);
                    continue;
                }

                PieceView created = Instantiate(source, source.transform.parent);
                created.gameObject.SetActive(true);
                created.ConfigureRuntimeIdentity(team, slot, GameHudPanel.GetTeamColor(team));
                pieceViews.Add(created);
            }
        }

        pieceViews.RemoveAll(view => !view.gameObject.activeSelf);
    }

    // 팀의 말 수 (노장이면 1개 적게). 증강이 확정된 뒤에만 의미가 있다
    private int PieceCount(int team) => VeteranAugment.PieceCountFor(boardState, team, setup.piecesPerTeam);

    private void CreateCoreState()
    {
        teamCount = setup.teamCount;
        boardView.Graph.AllowSkipShortcut = setup.allowSkipShortcut;
        boardState = new BoardState(boardView.Graph);
        gameState = new GameState(boardState);
        turnManager = new TurnManager(gameState, teamCount);
        gameEvents = new GameEvents();
        soundBinder = new GameSoundBinder(gameEvents);
        gameRules = new GameRules(
            gameState,
            turnManager,
            gameEvents,
            new YutThrower(new SystemRandomProvider()),
            new StandardGetResult(),
            CreateAugmentPool());
        // Installer가 없는 기존 단독 씬은 로컬 세션으로 계속 실행한다.
        // 서버 게임은 GameInstaller가 주입한 Factory를 통해 RemoteGameSession을 생성한다.
        gameSession = sessionFactory != null
            ? sessionFactory.Create(gameRules, gameEvents, gameState, turnManager)
            : new LocalGameSession(gameRules, gameEvents);
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

    private void OnSnapshotApplied(GameSnapshotMessage snapshot)
    {
        if (leavingGame) return;
        isMoving = false;
        pendingTurnTeam = null;
        pendingWinnerTeam = null;
        foreach (PieceView view in pieceViews)
        {
            view.CancelMove();
            if (view.Piece != null && view.Piece.IsOnBoard) view.gameObject.SetActive(true);
            if (view.Piece != null && !view.Piece.IsOnBoard) view.ReturnHome();
        }
        for (int team = 0; team < teamCount; team++)
            if (!boardState.Pieces.Any(piece => piece.Team == team))
                hud?.SetPlayerConnectionState(team, PlayerStatusView.ConnectionState.Removed);
        OnBoardLayoutReloaded();
        hudPresenter.RefreshStoredResults();
        lastStateHash = StateHasher.HashHex(gameState, turnManager);
        if (boardState.Pieces.Any(piece => piece.Team == setup.localTeam))
            hud?.SetPlayerConnectionState(setup.localTeam, PlayerStatusView.ConnectionState.Connected);

        if (snapshot.phase == "ENDED")
        {
            OnGameEnded(snapshot.winnerTeam ?? -1);
        }
        else if (snapshot.phase == "AUGMENT_SELECT")
        {
            augmentSelectionTeam = setup.localTeam;
            localAugmentConfirmed = snapshot.offeredAugments == null || snapshot.offeredAugments.Count == 0;
            currentAugmentChoices = snapshot.offeredAugments ?? new List<string>();
            ChangeState(GameFlowState.AugmentSelect);
            if (!localAugmentConfirmed) OnAugmentChoices(setup.localTeam, currentAugmentChoices);
            else
            {
                uiManager?.Close<AugmentSelectPanel>();
                hud?.SetInstruction("선택 완료! 다른 플레이어의 선택을 기다리는 중입니다.");
            }
        }
        else
        {
            uiManager?.Close<AugmentSelectPanel>();
            bool canThrow = gameState.RemainingThrows > 0 || gameState.StoredResults.Count == 0;
            ChangeState(canThrow ? GameFlowState.Throw : GameFlowState.Move);
        }
        if (!string.IsNullOrEmpty(snapshot.timerType) && snapshot.phase != "ENDED")
        {
            hud?.SetTimerRemaining(snapshot.timerRemainingSec);
            hud?.SetTimerRunning(true);
        }
    }

    private void SubscribeEvents()
    {
        gameSession.Thrown += OnThrown;
        gameSession.Moved += OnMoved;
        gameSession.TurnChanged += OnTurnChanged;
        gameSession.AugmentChoices += OnAugmentChoices;
        gameSession.AugmentSelected += OnAugmentSelectedEvent;
        gameSession.GameEnded += OnGameEnded;
        gameSession.RequestRejected += OnRequestRejected;
        boardState.PiecesReturnedByBoardChange += OnPiecesReturnedByBoardChange;
        boardState.DesyncDetected += OnDesyncDetected;
        boardView.LayoutReloaded += OnBoardLayoutReloaded;
        if (gameSession is RemoteGameSession remote)
        {
            remote.PlayerDisconnected += OnRemotePlayerDisconnected;
            remote.PlayerReconnected += OnRemotePlayerReconnected;
            remote.PlayerRemoved += OnRemotePlayerRemoved;
            remote.AutomatedMoveApplied += OnAutomatedMoveApplied;
            remote.ConnectionAvailabilityChanged += OnConnectionAvailabilityChanged;
            remote.SnapshotApplied += OnSnapshotApplied;
        }
    }

    private void OnDestroy()
    {
        soundBinder?.Dispose();
        if (hud != null)
        {
            hud.ThrowClicked -= OnThrowButtonClicked;
            hud.RethrowClicked -= OnRethrowButtonClicked;
            hud.WaitingPieceClicked -= OnWaitingPieceClicked;
            hud.SettingsClicked -= OnSettingsClicked;
        }
        if (settingsPanel != null) settingsPanel.ExitGameRequested -= OnExitGameRequested;
        if (resultPopup != null)
        {
            resultPopup.MainMenuClicked -= OnExitGameRequested;
            resultPopup.ReturnToRoomClicked -= OnReturnToRoomRequested;
        }

        if (gameSession != null)
        {
            if (gameSession is RemoteGameSession remote)
            {
                remote.PlayerDisconnected -= OnRemotePlayerDisconnected;
                remote.PlayerReconnected -= OnRemotePlayerReconnected;
                remote.PlayerRemoved -= OnRemotePlayerRemoved;
                remote.AutomatedMoveApplied -= OnAutomatedMoveApplied;
                remote.ConnectionAvailabilityChanged -= OnConnectionAvailabilityChanged;
                remote.SnapshotApplied -= OnSnapshotApplied;
            }
            gameSession.Thrown -= OnThrown;
            gameSession.Moved -= OnMoved;
            gameSession.TurnChanged -= OnTurnChanged;
            gameSession.AugmentChoices -= OnAugmentChoices;
            gameSession.AugmentSelected -= OnAugmentSelectedEvent;
            gameSession.GameEnded -= OnGameEnded;
            gameSession.RequestRejected -= OnRequestRejected;
            if (gameSession is IDisposable disposable) disposable.Dispose();
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
        if (leavingGame || gameState.WinnerTeam.HasValue || currentState != GameFlowState.Move || isMoving || gameSession == null ||
            gameSession.IsCommandPending || !setup.CanControl(CurrentTeam) ||
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
        if (!CanSendCommand(GameFlowState.Throw, CurrentTeam)) return;
        gameSession.RequestThrow();
        RefreshSessionInteraction();
    }

    public void OnRethrowButtonClicked()
    {
        if (!CanSendCommand(GameFlowState.Move, CurrentTeam)) return;
        gameSession.RequestRethrow();
        RefreshSessionInteraction();
    }

    public void OnAugmentSelected(string augmentId)
    {
        if (!CanSendCommand(GameFlowState.AugmentSelect, augmentSelectionTeam)) return;
        gameSession.RequestAugment(augmentId);
        RefreshSessionInteraction();
    }

    private void OnThrown(YutThrowOutcome outcome)
    {
        if (leavingGame) return;
        bool grantedExtraThrow = hudPresenter.DidGrantExtraThrow(outcome, CurrentTeam);
        hud?.ShowThrowResult(outcome, grantedExtraThrow, CurrentTeam);
        hudPresenter.RefreshStoredResults();
        hudPresenter.RefreshPlayerPanels();

        if (gameState.RemainingThrows > 0)
        {
            ChangeState(GameFlowState.Throw);
            hud?.SetInstruction($"{GameFlowHudPresenter.ResultLabel(outcome.Result)}! 한 번 더 던지세요.");
        }
        // 이동할 말이 없어도 야바위꾼을 쓸 수 있으면 일반 던지기를 끄고 재던지기를 표시한다.
        else if (gameRules.HasAnyStoredMove() || hudPresenter.CanUseRethrow(CurrentTeam))
        {
            ChangeState(GameFlowState.Move);
        }
    }

    private void OnAugmentSelectedEvent(int team, IAugment augment)
    {
        hudPresenter.RefreshPlayerPanels();
        if (gameSession is RemoteGameSession)
        {
            // 다른 팀의 확정 결과는 HUD만 갱신하고 내 선택 창을 유지한다.
            if (team == setup.localTeam && currentState == GameFlowState.AugmentSelect)
            {
                localAugmentConfirmed = true;
                uiManager?.Close<AugmentSelectPanel>();
                hud?.SetInstruction("선택 완료! 다른 플레이어의 선택을 기다리는 중입니다.");
            }
            return; // PLAYING 진입은 서버 TURN_CHANGED로 확정한다.
        }
        uiManager?.Close<AugmentSelectPanel>();

        if (team >= teamCount - 1)
        {
            augmentSelectionTeam = 0;
            CreatePieces();
            ChangeState(GameFlowState.Throw);
        }
        else
        {
            hud?.SetInstruction("증강 선택지를 기다리는 중입니다.");
        }
    }

    private void OnSettingsClicked()
    {
        settingsPanel = uiManager?.Open<SettingsPanel>();
        settingsPanel?.SetExitGameVisible(true);
    }

    private void BindSettingsPanel()
    {
        settingsPanel = uiManager?.Get<SettingsPanel>();
        if (settingsPanel == null) return;
        settingsPanel.SetExitGameVisible(true);
        settingsPanel.ExitGameRequested += OnExitGameRequested;
    }

    private void OnReturnToRoomRequested()
    {
        if (!BeginLeavingGame()) return;
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator != null) coordinator.ReturnToWaitingRoom();
        else SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnExitGameRequested()
    {
        if (!BeginLeavingGame()) return;
        settingsPanel?.Close();
        LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator != null)
        {
            coordinator.LeaveGameToMainMenu();
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private bool BeginLeavingGame()
    {
        if (leavingGame) return false;
        leavingGame = true;
        isMoving = false;
        pendingTurnTeam = null;
        pendingWinnerTeam = null;
        foreach (PieceView view in pieceViews) if (view != null) view.CancelMove();
        hud?.SetInteractionState(false, true);
        hud?.SetThrowInteractable(false);
        hud?.SetRethrowVisible(false);
        hud?.SetTimerRunning(false);
        hud?.SetInstruction("로비로 이동하는 중입니다...");
        return true;
    }

    // 로비/GAME_START 담당 연결 지점:
    // 서버가 playerId와 team 목록을 GAME_START에 포함하면 게임 시작 전에 이 메서드로 등록한다.
    public void RegisterPlayerTeam(string playerId, int team)
    {
        if (string.IsNullOrEmpty(playerId) || team < 0 || team >= (setup?.teamCount ?? teamCount)) return;
        playerTeams[playerId] = team;
    }

    private void OnRemotePlayerDisconnected(string playerId, int graceSec)
    {
        if (playerTeams.TryGetValue(playerId, out int team))
            hud?.SetPlayerConnectionState(team, PlayerStatusView.ConnectionState.Reconnecting, graceSec);
        hud?.SetInstruction("연결이 끊긴 플레이어를 기다리는 중입니다.");
    }

#if UNITY_EDITOR
    [ContextMenu("테스트/플레이어 4 연결 끊김")]
    private void TestPlayer4Disconnected()
    {
        if (!Application.isPlaying || hud == null)
        {
            Debug.LogWarning("[GameFlow] 플레이 모드에서만 연결 끊김 UI를 테스트할 수 있습니다.", this);
            return;
        }

        if (teamCount < 4)
        {
            Debug.LogWarning("[GameFlow] 플레이어 4 연결 끊김 테스트는 4인 게임에서만 표시됩니다.", this);
            return;
        }

        hud.SetPlayerConnectionState(3, PlayerStatusView.ConnectionState.Reconnecting, 30);
        hud.SetInstruction("연결이 끊긴 플레이어를 기다리는 중입니다.");
    }

    [ContextMenu("테스트/플레이어 4 재접속")]
    private void TestPlayer4Reconnected()
    {
        if (!Application.isPlaying || hud == null) return;
        hud.SetPlayerConnectionState(3, PlayerStatusView.ConnectionState.Connected);
        hud.SetInstruction("플레이어가 재접속했습니다.");
    }
#endif

    private void OnRemotePlayerReconnected(string playerId)
    {
        if (playerTeams.TryGetValue(playerId, out int team))
            hud?.SetPlayerConnectionState(team, PlayerStatusView.ConnectionState.Connected);
        hud?.SetInstruction("플레이어가 재접속했습니다.");
    }

    private void OnRemotePlayerRemoved(string playerId, int team)
    {
        hud?.SetPlayerConnectionState(team, PlayerStatusView.ConnectionState.Removed);
        selectionController?.Clear();
        foreach (PieceView view in pieceViews)
            if (view != null && view.Team == team) view.SetWorldVisible(false);

        if (!isMoving) RefreshPieceLayout();
        hudPresenter?.RefreshPlayerPanels();

        hud?.SetInstruction($"{PlayerName(team)}님이 게임에서 나갔습니다.");
    }

    private void OnAutomatedMoveApplied(int team)
    {
        hud?.SetInstruction($"{PlayerName(team)} 자동 진행 중");
    }

    private void OnConnectionAvailabilityChanged(bool connected)
    {
        hud?.SetConnectionAvailable(connected);
        if (!connected)
        {
            selectionController?.Clear();
            hud?.SetTimerRunning(false);
            hud?.SetPlayerConnectionState(setup.localTeam, PlayerStatusView.ConnectionState.Reconnecting);
            hud?.SetInstruction("서버 재접속을 기다리는 중입니다.");
            return;
        }

        hud?.SetTimerRunning(currentState == GameFlowState.Throw || currentState == GameFlowState.Move ||
            (currentState == GameFlowState.AugmentSelect && gameSession is RemoteGameSession remote &&
             remote.AugmentSelectionTimeLimitSec > 0));
        hud?.SetInstruction("서버에 다시 연결되었습니다.");
        RefreshSessionInteraction();
    }

    private void OnAugmentChoices(int team, IReadOnlyList<string> ids)
    {
        if (gameSession is RemoteGameSession && team != setup.localTeam) return;
        currentAugmentChoices = ids;
        localAugmentConfirmed = false;
        augmentSelectionTeam = team;
        if (currentState != GameFlowState.AugmentSelect) ChangeState(GameFlowState.AugmentSelect);
        OpenAugmentSelection(ids);
        if (gameSession is RemoteGameSession remote && remote.AugmentSelectionTimeLimitSec > 0)
        {
            hud?.SetTimerRemaining(remote.AugmentSelectionTimeLimitSec);
            hud?.SetTimerRunning(true);
        }
    }

    // 모든 팀의 증강이 확정된 뒤 팀별 말 수로 말을 만든다. (노장 증강을 보유한 팀은 마지막 슬롯 제외)
    private void CreatePieces()
    {
        if (piecesCreated) return;
        piecesCreated = true;
        EnsurePieceViewsForSetup();
        BindPieces();
        RefreshPieceLayout();
        hudPresenter.RefreshPlayerPanels();
        lastStateHash = StateHasher.HashHex(gameState, turnManager);
    }

    private void OnRequestRejected(string reason)
    {
        Debug.LogWarning($"[GameFlow] 요청이 거절됐습니다. {reason}", this);
        selectionController?.Clear();
        // 이동은 MOVE_APPLIED를 받아야 적용되므로, 거절되면 말 화면만 원래 상태로 되돌리면 된다
        if (!isMoving) RefreshPieceLayout();
        hudPresenter?.RefreshStoredResults();
        hudPresenter?.RefreshPlayerPanels();
        RefreshSessionInteraction();
        if (gameSession is RemoteGameSession && currentState == GameFlowState.AugmentSelect &&
            !localAugmentConfirmed && currentAugmentChoices.Count > 0)
            OpenAugmentSelection(currentAugmentChoices);
        hud?.SetInstruction(reason);
    }

    private void OnTurnChanged(int team)
    {
        if (leavingGame) return;
        hud?.ResetTimer();
        hudPresenter.RefreshStoredResults();
        hudPresenter.RefreshPlayerPanels();
        if (isMoving)
        {
            pendingTurnTeam = team;
            return;
        }
        bool canThrow = gameState.RemainingThrows > 0 || gameState.StoredResults.Count == 0;
        ChangeState(canThrow ? GameFlowState.Throw : GameFlowState.Move);
    }

    private void OnGameEnded(int winnerTeam)
    {
        if (leavingGame) return;
        if (currentState == GameFlowState.Finish)
        {
            if (resultPopup != null && gameSession is RemoteGameSession remote)
                resultPopup.SetRankings(remote.Rankings, playerProfiles,
                    (iconId, team) => hud?.ResolveProfileIcon(iconId, team), setup.localTeam);
            return;
        }
        pendingWinnerTeam = winnerTeam;
        if (!isMoving) ChangeState(GameFlowState.Finish);
    }

    private void ChangeState(GameFlowState next)
    {
        if (leavingGame || (next == GameFlowState.Finish && currentState == GameFlowState.Finish)) return;
        if (next != GameFlowState.AugmentSelect) uiManager?.Close<AugmentSelectPanel>();
        currentState = next;

        switch (next)
        {
            case GameFlowState.GameStart:
                augmentSelectionTeam = 0;
                if (gameSession is RemoteGameSession)
                {
                    augmentSelectionTeam = setup.localTeam;
                    ChangeState(GameFlowState.AugmentSelect);
                    break;
                }
                if (skipAugmentSelection)
                {
                    CreatePieces();
                    ChangeState(GameFlowState.Throw);
                }
                else
                {
                    ChangeState(GameFlowState.AugmentSelect);
                    if (gameSession is LocalGameSession localSession)
                        localSession.BeginAugmentSelection(teamCount);
                }
                break;

            case GameFlowState.Throw:
                PrepareInteractiveState(false);
                hud?.SetInteractionState(setup.CanControl(CurrentTeam), false);
                hud?.SetTurn($"{PlayerName(CurrentTeam)} 차례", CurrentTeam);
                hud?.SetInstruction(gameState.StoredResults.Count > 0 ? "추가 윷을 던지세요." : "윷을 던지세요.");
                hud?.SetThrowInteractable(gameState.RemainingThrows > 0);
                hud?.SetRethrowVisible(false);
                hud?.SetTimerRunning(true);
                break;

            case GameFlowState.Move:
                PrepareInteractiveState(true);
                hud?.SetInteractionState(setup.CanControl(CurrentTeam), false);
                hud?.SetTurn($"{PlayerName(CurrentTeam)} 차례", CurrentTeam);
                hud?.SetInstruction("이동할 말을 선택하세요.");
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
                hud?.SetInstruction($"{PlayerName(winner)} win");
                hud?.SetThrowInteractable(false);
                hud?.SetRethrowVisible(false);
                hud?.SetTimerRunning(false);
                resultPopup = uiManager?.Open<ResultPopup>()?.SetWinner(
                    PlayerName(winner),
                    GameHudPanel.GetTeamColor(winner),
                    hud?.ResolveProfileIcon(playerProfiles.FirstOrDefault(profile => profile != null && profile.team == winner)?.iconId, winner));
                if (resultPopup != null)
                {
                    resultPopup.MainMenuClicked -= OnExitGameRequested;
                    resultPopup.MainMenuClicked += OnExitGameRequested;
                    resultPopup.ReturnToRoomClicked -= OnReturnToRoomRequested;
                    resultPopup.ReturnToRoomClicked += OnReturnToRoomRequested;
                }
                if (resultPopup != null && gameSession is RemoteGameSession remoteSession)
                    resultPopup.SetRankings(remoteSession.Rankings, playerProfiles,
                        (iconId, team) => hud?.ResolveProfileIcon(iconId, team), setup.localTeam);
                break;

            case GameFlowState.AugmentSelect:
                PrepareAugmentSelection();
                break;
        }
    }

    private void PrepareInteractiveState(bool allowPieceSelection)
    {
        selectionController.Clear();
        selectionController.RefreshTurnHighlights(allowPieceSelection);
        hudPresenter.RefreshPlayerPanels();
    }

    private bool CanSendCommand(GameFlowState requiredState, int team)
    {
        return !leavingGame && !gameState.WinnerTeam.HasValue && currentState == requiredState && gameSession != null &&
               !gameSession.IsCommandPending && setup.CanControl(team);
    }

    private void RefreshSessionInteraction()
    {
        if (leavingGame || hud == null || gameSession == null) return;

        int controlledTeam = currentState == GameFlowState.AugmentSelect
            ? augmentSelectionTeam
            : CurrentTeam;
        hud.SetInteractionState(setup.CanControl(controlledTeam), gameSession.IsCommandPending);
    }

    private void PrepareAugmentSelection()
    {
        selectionController.Clear();
        hud?.SetTurn($"{PlayerName(augmentSelectionTeam)} 증강 선택", augmentSelectionTeam);
        hud?.SetThrowInteractable(false);
        hud?.SetRethrowVisible(false);
        // 시간 초과 선택은 서버가 처리하고 클라이언트는 전달받은 시간만 표시한다.
        hud?.SetTimerRunning(gameSession is RemoteGameSession remote && remote.AugmentSelectionTimeLimitSec > 0);
        hud?.SetInteractionState(setup.CanControl(augmentSelectionTeam), gameSession?.IsCommandPending ?? false);
        hud?.SetInstruction("증강 선택지를 기다리는 중입니다.");
    }

    private string PlayerName(int team)
    {
        foreach (PlayerDisplayInfo profile in playerProfiles)
        {
            if (profile != null && profile.team == team && !string.IsNullOrWhiteSpace(profile.nickname))
                return profile.nickname;
        }

        return team switch
        {
            0 => "빨강 팀",
            1 => "파랑 팀",
            2 => "초록 팀",
            _ => "노랑 팀",
        };
    }

    private void OpenAugmentSelection(IReadOnlyList<string> ids)
    {
        PrepareAugmentSelection();
        AugmentSelectPanel panel = uiManager != null ? uiManager.Get<AugmentSelectPanel>() : null;
        if (panel == null)
        {
            Debug.LogError("[GameFlow] AugmentSelectPanel이 UIManager에 등록되지 않았습니다.", this);
            return;
        }

        if (!setup.CanControl(augmentSelectionTeam))
        {
            uiManager?.Close<AugmentSelectPanel>();
            hud?.SetInstruction("다른 플레이어의 증강 선택을 기다리는 중입니다.");
            return;
        }

        List<AugmentDefinition> selection = new();
        foreach (string id in ids)
            selection.Add(augmentCatalog.Find(id));
        panel.Setup(selection, OnAugmentSelected);
        uiManager.Open<AugmentSelectPanel>();
        hud?.SetInstruction("증강을 선택하세요.");
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

        if (entersBoard) movingView.SetWorldVisible(false);
        gameSession.RequestMove(movingView.Piece.Id, option.ResultIndex, option.DestinationNodeId);
        RefreshSessionInteraction();
    }

    private void OnMoved(MoveResult result)
    {
        if (leavingGame) return;
        var points = new List<Vector3>();
        foreach (BoardNode node in result.Path.Nodes)
        {
            // 골인은 참먹이(보이는 출발 칸)를 통과한 뒤 판 밖 Start 노드로 처리된다.
            // 이동 연출은 참먹이에서 끝내고, 규칙상의 Start 이동과 완주 판정은 그대로 유지한다.
            if (result.Path.IsFinished && node.Type == BoardNodeType.Start) continue;

            NodeView view = boardView.GetView(node);
            if (view != null) points.Add(view.Position);
        }

        isMoving = true;
        int remaining = result.MovedPieces.Count;
        if (remaining == 0) { FinishMove(result); return; }
        foreach (Piece piece in result.MovedPieces)
        {
            if (!views.TryGetValue(piece, out PieceView view) || view == null)
            {
                if (--remaining == 0) FinishMove(result);
                continue;
            }
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
        if (leavingGame) return;
        isMoving = false;
        foreach (Piece captured in result.CapturedPieces)
            if (views.TryGetValue(captured, out PieceView capturedView))
                capturedView.ReturnHome();

        RefreshPieceLayout();
        hudPresenter.RefreshPlayerPanels();
        hudPresenter.RefreshStoredResults();
        lastStateHash = StateHasher.HashHex(gameState, turnManager);

        if (gameSession is RemoteGameSession && gameState.WinnerTeam.HasValue && !pendingWinnerTeam.HasValue)
        {
            // 로컬 완주 판정만으로 결과창을 열면 뒤늦은 GAME_ENDED가 창을 다시 연다.
            hud?.SetInteractionState(false, true);
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(false);
            hud?.SetTimerRunning(false);
            hud?.SetInstruction("경기 결과를 확인하는 중입니다...");
            return;
        }
        if (pendingWinnerTeam.HasValue || gameState.WinnerTeam.HasValue)
        {
            ChangeState(GameFlowState.Finish);
            return;
        }
        if (pendingTurnTeam.HasValue)
        {
            pendingTurnTeam = null;
            bool canThrow = gameState.RemainingThrows > 0 || gameState.StoredResults.Count == 0;
            ChangeState(canThrow ? GameFlowState.Throw : GameFlowState.Move);
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
        hud?.SetInstruction("게임 상태를 다시 동기화해야 합니다.");
    }

    private void OnBoardLayoutReloaded()
    {
        selectionController.Clear();
        RefreshPieceLayout();
        hudPresenter.RefreshPlayerPanels();
        selectionController.RefreshTurnHighlights(currentState == GameFlowState.Move);
        if (currentState == GameFlowState.Move) hud?.SetInstruction("이동할 말을 선택하세요.");
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
