using System;
using System.Collections.Generic;
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

        int waitingIndex = 0;
        foreach (PieceView view in pieceViews)
        {
            if (view.Team != team || view.Piece == null || !view.Piece.IsWaiting) continue;
            if (waitingIndex == slotIndex)
            {
                SelectPiece(view == SelectedPiece ? null : view);
                return;
            }
            waitingIndex++;
        }
    }

    public void Clear()
    {
        hud?.HideMoveOptionChoices();
        if (SelectedPiece != null) SelectedPiece.SetSelected(false);
        SelectedPiece = null;
        hud?.SetSelectedWaitingPiece(-1, -1);

        foreach (NodeView view in highlighted) view.ClearHighlight();
        highlighted.Clear();
        targets.Clear();

        foreach (GameObject label in labels)
            if (label != null) UnityEngine.Object.Destroy(label);
        labels.Clear();

        pathView?.ClearHighlights();
    }

    public void RefreshTurnHighlights(bool enabled = true)
    {
        foreach (PieceView view in pieceViews)
            view.SetTurnHighlighted(
                enabled &&
                view.Team == getCurrentTeam() &&
                view.Piece != null &&
                !view.Piece.IsFinished);
    }

    private void SelectPiece(PieceView piece)
    {
        Clear();
        SelectedPiece = piece;
        if (piece == null) return;

        piece.SetSelected(true);
        if (piece.Piece.IsWaiting)
            hud?.SetSelectedWaitingPiece(piece.Team, GetWaitingSlotIndex(piece));

        hud?.SetInstruction("Select a destination.");
        presenter.RefreshStoredResults(piece.Piece);

        foreach (MoveOption option in gameRules.GetMoveOptions(piece.Piece.Id))
        {
            BoardNode node = boardState.Graph.GetNode(option.DestinationNodeId);
            NodeView destination = boardView.GetView(node);
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
            pair.Key.SetDestinationBadge(GetDestinationBadge(pair.Key, pair.Value, piece.Team), piece.Team);
            pair.Key.SetHighlight(color);
            highlighted.Add(pair.Key);
            CreateLabel(pair.Key, pair.Value);
        }
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
        int index = 0;
        foreach (PieceView view in pieceViews)
        {
            if (view.Team != target.Team || view.Piece == null || !view.Piece.IsWaiting) continue;
            if (view == target) return index;
            index++;
        }
        return -1;
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
        float bestDistance = float.MaxValue;
        foreach (BoardNode node in boardView.Graph.Nodes)
        {
            NodeView view = boardView.GetView(node);
            if (view == null) continue;

            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>();
            float radius = sprite != null ? sprite.bounds.extents.x : 0.5f;
            float distance = Vector2.Distance(world, view.Position);
            if (distance <= radius && distance < bestDistance)
            {
                best = view;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void CreateLabel(NodeView destination, IReadOnlyList<MoveOption> options)
    {
        var labelObject = new GameObject("MoveResultLabel");
        labelObject.transform.SetParent(destination.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.02f, 0f);

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
            string name = GameFlowHudPresenter.MoveOptionLabel(option);
            if (!names.Contains(name)) names.Add(name);
        }

        text.text = string.Join("/", names);
        text.color = options.Count == 1 ? GameFlowHudPresenter.ResultColor(options[0].Result) : Color.white;
        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        renderer.sortingOrder = 35;
        labels.Add(labelObject);
    }

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

    private static bool AreEquivalent(IReadOnlyList<MoveOption> options)
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
}
