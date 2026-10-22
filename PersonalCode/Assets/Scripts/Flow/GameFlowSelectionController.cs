using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 월드의 말과 노드 선택, 목적지 후보 표시를 담당한다.
/// 실제 이동 성공 여부와 결과 소비는 GameRules와 GameFlowController에 위임한다.
/// </summary>
public sealed class GameFlowSelectionController
{
    private readonly BoardView boardView;
    private readonly BoardState boardState;
    private readonly GameRules gameRules;
    private readonly GameHudPanel hud;
    private readonly PathView pathView;
    private readonly IReadOnlyList<PieceView> pieceViews;
    private readonly Func<int> getCurrentTeam;
    private readonly Action<MoveOption> moveUsingOption;
    private readonly GameFlowHudPresenter presenter;
    private readonly GameObject destinationLabelPrefab;

    private readonly Dictionary<NodeView, List<MoveOption>> targets = new();
    private readonly HashSet<NodeView> highlighted = new();
    private readonly List<GameObject> labels = new();

    public PieceView SelectedPiece { get; private set; }

    public GameFlowSelectionController(
        BoardView boardView,
        BoardState boardState,
        GameRules gameRules,
        GameHudPanel hud,
        PathView pathView,
        IReadOnlyList<PieceView> pieceViews,
        Func<int> getCurrentTeam,
        Action<MoveOption> moveUsingOption,
        GameFlowHudPresenter presenter)
    {
        this.boardView = boardView;
        this.boardState = boardState;
        this.gameRules = gameRules;
        this.hud = hud;
        this.pathView = pathView;
        this.pieceViews = pieceViews;
        this.getCurrentTeam = getCurrentTeam;
        this.moveUsingOption = moveUsingOption;
        this.presenter = presenter;
        destinationLabelPrefab = Resources.Load<GameObject>("UI/DestinationResultLabel");
    }

    public void HandleClick(Vector3 world)
    {
        NodeView node = PickNode(world);
        PieceView clicked = PickPiece(world);

        if (SelectedPiece != null)
        {
            NodeView target = node;
            if ((target == null || !targets.ContainsKey(target)) && clicked != null && clicked.Piece.IsOnBoard)
                target = boardView.GetView(clicked.Piece.Node);

            if (target != null && targets.TryGetValue(target, out List<MoveOption> options) && options.Count > 0)
            {
                UseDestinationOptions(options);
                return;
            }
        }

        if (clicked != null && clicked.Team == getCurrentTeam() && !clicked.Piece.IsFinished)
            SelectPiece(clicked == SelectedPiece ? null : clicked);
    }

    public void HandleWaitingPieceClick(int team, int slotIndex)
    {
        if (team != getCurrentTeam()) return;

        foreach (PieceView view in pieceViews)
        {
            if (view.Team == team && view.SlotIndex == slotIndex &&
                view.Piece != null && view.Piece.IsWaiting)
            {
                SelectPiece(view == SelectedPiece ? null : view);
                return;
            }
        }
    }

    public void Clear()
    {
        hud?.HideMoveOptionChoices();
        foreach (PieceView view in pieceViews)
            if (view != null) view.SetSelected(false);
        SelectedPiece = null;
        hud?.SetSelectedWaitingPiece(-1, -1);

        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        targets.Clear();

        foreach (PieceView view in pieceViews)
            if (view != null) hud?.SetWaitingPieceAvailable(view.Team, view.SlotIndex, false);

        foreach (GameObject label in labels)
            if (label != null) UnityEngine.Object.Destroy(label);
        labels.Clear();

        pathView?.ClearHighlights();
    }

    public void RefreshTurnHighlights(bool enabled = true)
    {
        foreach (PieceView view in pieceViews)
        {
            bool canMove = enabled && view.Team == getCurrentTeam() &&
                           view.Piece != null && !view.Piece.IsFinished &&
                           gameRules.GetMoveOptions(view.Piece.Id).Count > 0;
            view.SetTurnHighlighted(
                canMove);
            hud?.SetWaitingPieceAvailable(view.Team, view.SlotIndex, canMove && view.Piece.IsWaiting);
        }
    }

    private void SelectPiece(PieceView piece)
    {
        Clear();
        SelectedPiece = piece;
        if (piece == null) return;

        // 업힌 말들은 숫자 배지도 겹쳐 있으므로 묶음 전체를 같은 비율로 확대한다.
        List<Piece> stack = boardState.GetStack(piece.Piece);
        foreach (PieceView view in pieceViews)
            if (view != null && view.Piece != null && stack.Contains(view.Piece))
                view.SetSelected(true);
        if (piece.Piece.IsWaiting)
            hud?.SetSelectedWaitingPiece(piece.Team, GetWaitingSlotIndex(piece));

        hud?.SetInstruction("이동할 칸을 선택하세요.");
        presenter.RefreshStoredResults(piece.Piece);

        foreach (MoveOption option in gameRules.GetMoveOptions(piece.Piece.Id))
        {
            BoardNode node = boardState.Graph.GetNode(option.DestinationNodeId);
            NodeView destination = option.IsFinished ? FindVisibleGoalEntry() : boardView.GetView(node);
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
            Color color = pair.Value.Count == 1
                ? GameFlowHudPresenter.ResultColor(pair.Value[0].Result)
                : Color.white;
            //pair.Key.SetDestinationBadge(GetDestinationBadge(pair.Key, pair.Value, piece.Team), piece.Team);
            pair.Key.SetHighlight(color, piece.Team);
            highlighted.Add(pair.Key);
            CreateLabel(pair.Key, pair.Value);
        }
    }

    // Core와 서버에는 골인 목적지를 Start 노드 ID로 그대로 전달한다.
    // Start 노드는 판 밖에 있으므로, 화면에서는 참먹이(보이는 출발 칸)를 골인 선택 지점으로 사용한다.
    private NodeView FindVisibleGoalEntry()
    {
        foreach (NodeView view in boardView.GetNodeViews())
            if (view.Type == BoardNodeType.End)
                return view;

        return boardView.GetView(boardState.Graph.StartNode);
    }

    private void UseDestinationOptions(IReadOnlyList<MoveOption> options)
    {
        if (options.Count == 1 || AreEquivalent(options))
        {
            moveUsingOption(options[0]);
            return;
        }

        var choices = new List<MoveOption>(options);
        var optionLabels = new List<string>(choices.Count);
        foreach (MoveOption option in choices)
            optionLabels.Add(GameFlowHudPresenter.MoveOptionLabel(option));

        hud?.ShowMoveOptionChoices(optionLabels, getCurrentTeam(), index =>
        {
            if (index >= 0 && index < choices.Count) moveUsingOption(choices[index]);
        });
    }

    private int GetWaitingSlotIndex(PieceView target)
    {
        return target != null ? target.SlotIndex : -1;
    }

    private PieceView PickPiece(Vector3 world)
    {
        PieceView best = null;
        float bestDistance = float.MaxValue;
        foreach (PieceView view in pieceViews)
        {
            if (view == null || !view.gameObject.activeInHierarchy || !view.Contains(world)) continue;

            float distance = view.DistanceTo(world);
            if (distance >= bestDistance) continue;
            best = view;
            bestDistance = distance;
        }
        return best;
    }

    private NodeView PickNode(Vector3 world)
    {
        NodeView best = null;
        float bestDistance = float.MaxValue;
        bool bestIsTarget = false;
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view == null) continue;

            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float distance = Vector2.Distance(world, view.Position);
            if (!ContainsNode(view, sprite, world)) continue;

            // 출발·도착처럼 겹친 노드는 현재 이동 가능한 목적지를 먼저 고른다.
            bool isTarget = targets.TryGetValue(view, out List<MoveOption> options) && options.Count > 0;
            if (best != null && (bestIsTarget && !isTarget ||
                bestIsTarget == isTarget && distance >= bestDistance)) continue;

            best = view;
            bestDistance = distance;
            bestIsTarget = isTarget;
        }
        return best;
    }

    private static bool ContainsNode(NodeView view, SpriteRenderer sprite, Vector3 world)
    {
        // 출발·도착 표시는 사각형이므로 보이는 영역의 모서리도 선택할 수 있다.
        if (sprite != null && (view.Type == BoardNodeType.Start || view.Type == BoardNodeType.End))
        {
            Bounds bounds = sprite.bounds;
            return world.x >= bounds.min.x && world.x <= bounds.max.x &&
                   world.y >= bounds.min.y && world.y <= bounds.max.y;
        }

        float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;
        return Vector2.Distance(world, view.Position) <= radius;
    }

    private void CreateLabel(NodeView destination, IReadOnlyList<MoveOption> options)
    {
        if (options.Count > 1) return; // 복수 결과의 목적지는 노드 강조만 유지한다.
        if (destinationLabelPrefab == null)
        {
            Debug.LogError("[GameFlow] Resources/UI/DestinationResultLabel 프리팹이 없습니다.");
            return;
        }
        GameObject labelObject = UnityEngine.Object.Instantiate(destinationLabelPrefab, destination.transform, false);
        labelObject.name = "MoveResultLabel";

        SpriteRenderer source = destination.GetComponentInChildren<SpriteRenderer>();
        if (source != null)
        {
            SpriteRenderer badge = labelObject.GetComponentInChildren<SpriteRenderer>();
            badge.sprite = destination.GetResultBadgeSprite(getCurrentTeam());
            if (badge.sprite == null) badge.sprite = source.sprite;
            badge.color = Color.white;
            badge.sortingLayerID = source.sortingLayerID;
            badge.sortingOrder = 34;
        }

        TextMeshPro text = labelObject.GetComponentInChildren<TextMeshPro>();

        text.text = GameFlowHudPresenter.ResultLabel(options[0].Result);
        text.color = Color.white;
        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (source != null) renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = 35;
        labels.Add(labelObject);
    }

    //private DestinationBadgeType GetDestinationBadge(
    //    NodeView destination,
    //    IReadOnlyList<MoveOption> options,
    //    int movingTeam)
    //{
    //    foreach (MoveOption option in options)
    //        if (option.IsFinished) return DestinationBadgeType.Finish;

    //    BoardNode node = destination.Node ?? boardState.Graph.GetNode(destination.Id);
    //    if (node == null) return DestinationBadgeType.None;

    //    bool hasSameTeam = false;
    //    foreach (Piece occupant in boardState.GetPiecesAt(node))
    //    {
    //        if (occupant.Team != movingTeam) return DestinationBadgeType.Capture;
    //        hasSameTeam = true;
    //    }

    //    return hasSameTeam ? DestinationBadgeType.Carry : DestinationBadgeType.None;
    //}

    private static bool AreEquivalent(IReadOnlyList<MoveOption> options)
    {
        if (options == null || options.Count < 2) return true;
        MoveOption first = options[0];
        for (int i = 1; i < options.Count; i++)
        {
            MoveOption other = options[i];
            // 같은 전진 이동은 결과 이름이 달라도 첫 결과를 사용한다.
            // 빽도는 남겨둘 가치와 사용 경로가 다르므로 도와 따로 선택한다.
            if ((other.Result == YutResult.BackDo) != (first.Result == YutResult.BackDo) ||
                other.ModifiedMoveCount != first.ModifiedMoveCount ||
                other.DestinationNodeId != first.DestinationNodeId ||
                other.IsFinished != first.IsFinished)
                return false;
        }
        return true;
    }
}
