using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameFlowState { GameStart, AugmentSelect, Throw, Move, EndTurn, Finish }

/// <summary>
/// LSU_Game의 입력과 화면을 Core 규칙(BoardState)에 연결하는 조립 담당 Controller.
///
/// 기본 흐름:
/// Throw → 결과 저장 → 말 선택 → 저장된 결과별 도착지 표시
/// → 도착지 선택/결과 소비 → 이동 → 추가 던지기 또는 턴 종료.
///
/// BoardState가 실제 이동·업기·잡기·증강 규칙을 처리하고,
/// 이 클래스는 상태 전환, 입력 전달, PieceView/HUD 갱신만 담당한다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<PieceView> pieceViews = new();
    [SerializeField] private PathView pathView;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private bool skipAugmentSelection = true;
    [SerializeField] private bool allowSkipShortcut;

    // 한 도착점에 도달할 수 있는 '저장 결과 + 경로' 조합.
    // 서로 다른 결과가 같은 노드에 도착할 수 있으므로 노드마다 목록으로 보관한다.
    private sealed class MoveOption
    {
        public YutResult Result;
        public BoardPath Path;
        public int ModifiedMoveCount;
    }
    private readonly Dictionary<Piece, PieceView> views = new();
    private readonly Dictionary<NodeView, List<MoveOption>> targets = new();
    private readonly HashSet<NodeView> highlighted = new();
    private readonly List<GameObject> labels = new();
    private readonly List<YutResult> storedResults = new();
    private readonly AugmentPool augmentPool = new();
    private BoardState state;
    private GameHudPanel hud;
    private PieceView selectedPiece;
    private Camera mainCamera;
    private GameFlowState currentState;
    private int currentTeam;
    private bool isMoving;

    public GameFlowState CurrentState => currentState;
    public int CurrentTeam => currentTeam;
    public IReadOnlyList<YutResult> StoredResults => storedResults;

    private void Start()
    {
        // 씬에 말이 추가되어도 Inspector 목록을 다시 채우지 않아도 되도록 자동 수집한다.
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
            hud.SetRethrowVisible(false);
        }

        boardView.Graph.AllowSkipShortcut = allowSkipShortcut;
        state = new BoardState(boardView.Graph);
        state.Moved += OnMoved;
        pieceViews.Sort((a, b) => a.Team != b.Team ? a.Team.CompareTo(b.Team) : string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < pieceViews.Count; i++)
        {
            Piece piece = state.AddPiece(i, pieceViews[i].Team);
            views.Add(piece, pieceViews[i]);
            pieceViews[i].Bind(piece);
            pieceViews[i].SetSortingOrder(10);
        }
        RegisterAugments();
        ChangeState(GameFlowState.GameStart);
    }

    private void OnDestroy()
    {
        if (hud != null)
        {
            hud.ThrowClicked -= OnThrowButtonClicked;
            hud.RethrowClicked -= OnRethrowButtonClicked;
        }
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
        // 윷/모와 잡기 때문에 결과가 여러 개 쌓일 수 있다.
        // 결과를 먼저 고르지 않고, 말과 도착지를 고를 때 대응하는 결과를 소비한다.
        storedResults.Add(outcome.Result);
        RefreshStoredResultText();
        if (outcome.Result.GrantsExtraThrow())
        {
            hud?.SetInstruction($"{ResultLabel(outcome.Result)}! Throw again.");
            return;
        }
        BeginMoveSelectionOrEndTurn();
    }

    // Rethrow 증강은 마지막 일반 결과를 버리고 Throw 상태로 되돌린다.
    // 윷/모로 얻은 이전 저장 결과는 유지되며, 증강은 성공한 순간 한 번만 소모된다.
    public void OnRethrowButtonClicked()
    {
        if (currentState != GameFlowState.Move || storedResults.Count == 0) return;

        YutResult latest = storedResults[storedResults.Count - 1];
        RethrowAugment rethrow = GetRethrowAugment();
        if (rethrow == null || !rethrow.TryUse(state, currentTeam, latest)) return;

        storedResults.RemoveAt(storedResults.Count - 1);
        RefreshStoredResultText();
        ChangeState(GameFlowState.Throw);
    }

    public void OnAugmentSelected(string augmentId)
    {
        // 증강 선택 UI는 문자열 ID만 넘기면 된다.
        // 실제 효과는 BoardState에 등록된 뒤 GetMovePaths/TryMove 과정에서 자동 적용된다.
        if (state == null) return;
        state.AddAugment(augmentPool.Pick(currentTeam, augmentId));
        ChangeState(GameFlowState.Throw);
    }

    private void RegisterAugments()
    {
        augmentPool.Register("BackDoSwitch", team => new BackDoSwitchAugment(team));
        augmentPool.Register("QuickStart", team => new QuickStartAugment(team));
        augmentPool.Register("WeakBoost", team => new WeakBoostAugment(team));
        augmentPool.Register("Shield", team => new ShieldAugment(team));
        augmentPool.Register("SturdyCarry", team => new SturdyCarryAugment(team));
        augmentPool.Register("Rethrow", team => new RethrowAugment(team));
    }

    private void BeginMoveSelectionOrEndTurn()
    {
        if (HasAnyStoredMove()) ChangeState(GameFlowState.Move);
        else
        {
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
            hud?.SetTurn($"Team {currentTeam + 1} Turn");
            hud?.SetInstruction(storedResults.Count > 0 ? "Bonus throw." : "Throw Yut.");
            hud?.SetThrowInteractable(true);
            hud?.SetRethrowVisible(false);
        }
        else if (next == GameFlowState.Move)
        {
            ClearSelection();
            RefreshTurnHighlights();
            hud?.SetTurn($"Team {currentTeam + 1} Turn");
            hud?.SetInstruction("Select a piece.");
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(CanUseRethrow());
        }
        else if (next == GameFlowState.EndTurn) EndTurn();
        else if (next == GameFlowState.Finish)
        {
            ClearSelection();
            RefreshTurnHighlights(false);
            hud?.SetInstruction($"Team {currentTeam + 1} wins!");
            hud?.SetRethrowVisible(false);
            uiManager?.Open<ResultPopup>()?.SetWinner($"Team {currentTeam + 1}", currentTeam == 0 ? Color.blue : Color.red);
        }
        else if (next == GameFlowState.AugmentSelect)
        {
            AugmentSelectPanel panel = uiManager.Get<AugmentSelectPanel>();
            panel.Setup(augmentPool, currentTeam, OnAugmentSelected);
            uiManager.Open<AugmentSelectPanel>();
//<<<<<<< HEAD
            hud?.SetThrowInteractable(false);
            hud?.SetRethrowVisible(false);
//=======
//>>>>>>> dev
        }
    }

    private void HandleClick(Vector3 world)
    {
        // 조작 순서: 자신의 말 클릭 → 강조된 도착 노드 클릭.
        // 도착 노드 위에 다른 말이 있어도 그 말을 누르면 해당 노드를 선택한 것으로 처리한다.
        NodeView node = PickNode(world);
        PieceView clicked = PickPiece(world);
        if (selectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !targets.ContainsKey(target)) && clicked != null && clicked.Piece.IsOnBoard)
                target = boardView.GetView(clicked.Piece.Node);
            if (target != null && targets.TryGetValue(target, out List<MoveOption> options) && options.Count > 0)
            {
                MoveUsingOption(options[0]);
                return;
            }
        }
        if (clicked != null && clicked.Team == currentTeam && !clicked.Piece.IsFinished)
            SelectPiece(clicked == selectedPiece ? null : clicked);
    }

    private void SelectPiece(PieceView piece)
    {
        ClearSelection();
        selectedPiece = piece;
        if (piece == null) return;
        piece.SetSelected(true);
        hud?.SetInstruction("Select a destination.");
        RefreshStoredResultText(piece.Piece);
        // 선택한 말에 대해 저장된 모든 윷 결과의 경로를 한 번에 계산한다.
        // 증강으로 경로/칸 수가 바뀌는 경우도 BoardState.GetMovePaths 결과에 반영된다.
        foreach (YutResult result in storedResults)
        {
            foreach (BoardPath path in state.GetMovePaths(piece.Piece, result.Steps()))
            {
                NodeView destination = boardView.GetView(path.Destination);
                if (destination == null) continue;
                if (!targets.TryGetValue(destination, out List<MoveOption> options))
                {
                    options = new List<MoveOption>();
                    targets.Add(destination, options);
                }
                options.Add(new MoveOption
                {
                    Result = result,
                    Path = path,
                    ModifiedMoveCount = state.GetModifiedMoveCount(piece.Piece, result.Steps())
                });
            }
        }
        foreach (KeyValuePair<NodeView, List<MoveOption>> pair in targets)
        {
            Color color = pair.Value.Count == 1 ? ResultColor(pair.Value[0].Result) : Color.white;
            pair.Key.SetHighlight(color);
            highlighted.Add(pair.Key);
            CreateLabel(pair.Key, pair.Value);
        }
    }

    private void MoveUsingOption(MoveOption option)
    {
        // 사용자가 선택한 도착지에 대응하는 결과 하나만 제거한다.
        // 같은 결과가 여러 번 저장되어 있으면 List.Remove가 한 개만 소비한다.
        Piece moving = selectedPiece.Piece;
        storedResults.Remove(option.Result);
        RefreshStoredResultText();
        ClearSelection();
        if (!state.TryMove(moving, option.Result.Steps(), option.Path.Destination, out _))
            BeginMoveSelectionOrEndTurn();
    }

    private void CreateLabel(NodeView destination, IReadOnlyList<MoveOption> options)
    {
        var go = new GameObject("MoveResultLabel");
        go.transform.SetParent(destination.transform, false);
        go.transform.localPosition = new Vector3(0f, 0.62f, 0f);
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
        // Core의 이동은 이미 끝난 상태다. 여기서는 계산된 노드 경로를 화면 애니메이션으로 재생한다.
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
            view.SetSortingOrder(20);
            view.PlayMove(points, result.Path.IsFinished, () => { if (--remaining == 0) FinishMove(result); });
        }
    }

    private void FinishMove(MoveResult result)
    {
        // Shield/SturdyCarry가 잡기 목록을 수정한 최종 결과만 화면에 반영한다.
        foreach (Piece captured in result.CapturedPieces) views[captured].ReturnHome();
        RefreshPieceLayout();
        isMoving = false;
        if (TeamFinished(currentTeam)) ChangeState(GameFlowState.Finish);
        else if (result.CapturedPieces.Count > 0) ChangeState(GameFlowState.Throw);
        else if (storedResults.Count > 0) BeginMoveSelectionOrEndTurn();
        else ChangeState(GameFlowState.EndTurn);
    }

    private void RefreshPieceLayout()
    {
        // 같은 팀 말은 업힌 묶음이므로 같은 좌표와 개수 표시를 사용한다.
        // Shield/SturdyCarry로 서로 다른 팀이 같은 노드에 남으면 팀 묶음을 좌우로 분리한다.
        foreach (PieceView view in pieceViews)
        {
            view.SetSortingOrder(10);
            if (view.Piece != null && view.Piece.IsOnBoard) view.SetStackCount(state.GetStack(view.Piece).Count);
        }
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            List<Piece> pieces = state.GetPiecesAt(node);
            var teams = new List<int>();
            foreach (Piece piece in pieces) if (!teams.Contains(piece.Team)) teams.Add(piece.Team);
            teams.Sort();
            for (int i = 0; i < teams.Count; i++)
            {
                float x = teams.Count == 1 ? 0f : (i - (teams.Count - 1) * 0.5f) * 0.55f;
                Vector3 position = boardView.GetView(node).Position + new Vector3(x, 0f, 0f);
                foreach (Piece piece in pieces) if (piece.Team == teams[i]) views[piece].SetBoardPosition(position);
            }
        }
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
        float distanceBest = float.MaxValue;
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view == null) continue;
            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;
            float distance = Vector2.Distance(world, view.Position);
            if (distance <= radius && distance < distanceBest) { best = view; distanceBest = distance; }
        }
        return best;
    }

    private void ClearSelection()
    {
        // 말 선택이 바뀌거나 상태가 전환될 때 임시 강조와 결과 라벨을 모두 정리한다.
        if (selectedPiece != null) selectedPiece.SetSelected(false);
        selectedPiece = null;
        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        targets.Clear();
        foreach (GameObject label in labels) if (label != null) Destroy(label);
        labels.Clear();
        if (pathView != null) pathView.ClearHighlights();
    }

    private void RefreshTurnHighlights(bool enabled = true)
    {
        foreach (PieceView view in pieceViews)
            view.SetTurnHighlighted(enabled && view.Team == currentTeam && view.Piece != null && !view.Piece.IsFinished);
    }

    private void RefreshStoredResultText(Piece contextPiece = null)
    {
        if (hud == null) return;
        if (storedResults.Count == 0) { hud.SetResults("Stored Results: -"); return; }
        var results = new List<string>();
        foreach (YutResult result in storedResults)
        {
            int baseCount = result.Steps();
            int modified = contextPiece != null ? state.GetModifiedMoveCount(contextPiece, baseCount) : baseCount;
            int bonus = modified - baseCount;
            string bonusText = bonus != 0 ? $" ({bonus:+0;-0})" : "";
            results.Add($"{ResultLabel(result)} {baseCount}{bonusText}");
        }
        hud.SetResults($"Stored: {string.Join("  ", results)}");
    }

    private RethrowAugment GetRethrowAugment()
    {
        if (state == null) return null;
        foreach (IAugment augment in state.GetAugments(currentTeam))
            if (augment is RethrowAugment rethrow) return rethrow;
        return null;
    }

    private bool CanUseRethrow()
    {
        if (storedResults.Count == 0) return false;
        RethrowAugment rethrow = GetRethrowAugment();
        return rethrow != null && rethrow.CanUse(state, currentTeam, storedResults[storedResults.Count - 1]);
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
}
