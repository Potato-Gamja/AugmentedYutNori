using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core 상태를 HUD에 표시할 문자열과 집계 값으로 변환한다.
/// GameFlowController가 UI 세부 형식과 증강 조회 방법을 알지 않도록 분리한 Presenter다.
/// </summary>
public sealed class GameFlowHudPresenter
{
    private readonly GameHudPanel hud;
    private readonly BoardState boardState;
    private readonly GameState gameState;
    private readonly AugmentCatalog augmentCatalog;
    private readonly IReadOnlyList<PieceView> pieceViews;

    public GameFlowHudPresenter(
        GameHudPanel hud,
        BoardState boardState,
        GameState gameState,
        AugmentCatalog augmentCatalog,
        IReadOnlyList<PieceView> pieceViews)
    {
        this.hud = hud;
        this.boardState = boardState;
        this.gameState = gameState;
        this.augmentCatalog = augmentCatalog;
        this.pieceViews = pieceViews;
    }

    public void RefreshPlayerPanels()
    {
        if (hud == null || boardState == null) return;

        int teamCount = 2;
        foreach (Piece piece in boardState.Pieces)
            teamCount = Mathf.Max(teamCount, piece.Team + 1);
        teamCount = Mathf.Min(teamCount, 4);

        for (int team = 0; team < teamCount; team++)
        {
            int waiting = 0;
            int onBoard = 0;
            int finished = 0;
            var waitingSlots = new List<int>();

            foreach (Piece piece in boardState.Pieces)
            {
                if (piece.Team != team) continue;
                if (piece.IsWaiting)
                {
                    waiting++;
                    PieceView view = FindView(piece);
                    if (view != null) waitingSlots.Add(view.SlotIndex);
                }
                else if (piece.IsOnBoard) onBoard++;
                else if (piece.IsFinished) finished++;
            }

            var augmentNames = new List<string>();
            foreach (IAugment augment in boardState.GetAugments(team))
                augmentNames.Add(augmentCatalog.Find(augment.Id).name);

            hud.SetPlayerState(team, waiting, onBoard, finished, augmentNames, waitingSlots);
        }
    }

    private PieceView FindView(Piece piece)
    {
        if (pieceViews == null) return null;
        foreach (PieceView view in pieceViews)
            if (view != null && view.Piece == piece) return view;
        return null;
    }

    public void RefreshStoredResults(Piece contextPiece = null)
    {
        if (hud == null || gameState == null) return;
        if (gameState.StoredResults.Count == 0)
        {
            hud.SetResults("저장 결과: -");
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

        hud.SetResults($"저장: {string.Join("  ", results)}");
    }

    public bool CanUseRethrow(int team)
    {
        if (gameState == null) return false;
        RethrowAugment rethrow = GetRethrowAugment(team);
        return rethrow != null && rethrow.CanUse(gameState, team);
    }

    public static string MoveOptionLabel(MoveOption option)
    {
        int baseCount = option.Result.Steps();
        int bonus = option.ModifiedMoveCount - baseCount;
        string bonusText = bonus != 0 ? $" ({bonus:+0;-0})" : "";
        return $"{ResultLabel(option.Result)} {baseCount}{bonusText}";
    }

    public static Color ResultColor(YutResult result) => result switch
    {
        YutResult.BackDo => new Color(1f, 0.25f, 1f),
        YutResult.Do => Color.yellow,
        YutResult.Gae => new Color(1f, 0.55f, 0.1f),
        YutResult.Geol => Color.red,
        YutResult.Yut => Color.green,
        YutResult.Mo => Color.cyan,
        _ => Color.white
    };

    public static string ResultLabel(YutResult result) => result switch
    {
        YutResult.BackDo => "빽도",
        YutResult.Do => "도",
        YutResult.Gae => "개",
        YutResult.Geol => "걸",
        YutResult.Yut => "윷",
        YutResult.Mo => "모",
        _ => "-"
    };

    private RethrowAugment GetRethrowAugment(int team)
    {
        if (boardState == null) return null;
        foreach (IAugment augment in boardState.GetAugments(team))
            if (augment is RethrowAugment rethrow) return rethrow;
        return null;
    }
}
