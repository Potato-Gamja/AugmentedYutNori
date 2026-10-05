using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class StateHasher 
{
    public static string BuildCanonical(GameState game, TurnManager turn)
    {
        var sb = new StringBuilder(256);

        //턴
        sb.Append("T").Append(I(turn.CurrentTeam)).Append("|R").Append(I(turn.Round));

        //이번 턴 진행 상태
        sb.Append("|RT").Append(I(game.RemainingThrows)).Append("|HM")
            .Append(game.HasMovedThisTurn ? '1' : '0')
            .Append("|W").Append(game.WinnerTeam.HasValue ? I(game.WinnerTeam.Value) : "-");

        //저장된 윷 결과
        sb.Append("|Y:");
        foreach (YutResult result in game.StoredResults)
            sb.Append(I((int)result)).Append(',');

        //말 정렬(ID순)
        sb.Append("|P:");
        foreach(Piece piece in SortedPieces(game.Board))
        {
            sb.Append(I(piece.Id)).Append(':')
              .Append(I(piece.Team)).Append(':')
              .Append(I((int)piece.State)).Append(':')
              .Append(piece.Node != null ? I(piece.Node.Id) : "-").Append(':')
              .Append(piece.CameFrom != null ? I(piece.CameFrom.Id) : "-")   // 갈림길 방향에 영향
              .Append(';');
        }

        sb.Append("|A:");
        foreach (IAugment augment in game.Board.Augments)
            sb.Append(I(augment.OwnerTeam)).Append(':').Append(augment.Id).Append(';');

        return sb.ToString();
    }

    public static ulong Hash(GameState game, TurnManager turn) => Fnv1a64(BuildCanonical(game, turn));

    public static string HashHex(GameState game, TurnManager turn) =>
        Hash(game, turn).ToString("x16", CultureInfo.InvariantCulture);

    private static ulong Fnv1a64(string text)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;
        foreach(byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }

    private static IEnumerable<Piece> SortedPieces(BoardState board) => board.Pieces.OrderBy(piece => piece.Id);

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
}
