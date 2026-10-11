using System.Collections.Generic;

// 판 위에 내 말을 한 개만 둘 수 있는 대신(업기 불가) 상대에게 잡히지 않는 증강
public class LonerAugment : Augment
{
    public override string Id => "Loner";

    public LonerAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyPaths(AugmentContext context, List<BoardPath> paths)
    {
        // 내 말이 아니거나, 판 위의 말이 움직이는 거라면 아무것도 하지 않는다
        if (!context.IsMine(this) || !context.IsWaiting)
            return;
        // 판 위에 내 말이 이미 있으면 대기 말은 출발할 수 없다 (판에 한 개만)
        IReadOnlyCollection<Piece> pieces = context.State.Pieces;
        foreach (Piece piece in pieces)
        {
            if (piece.Team == OwnerTeam && piece.IsOnBoard)
            {
                paths.Clear();
                return;
            }
        }
    }

    public override void ModifyCaptures(AugmentContext context, List<Piece> captured)
    {
        // 내 이동이면 아무것도 하지 않는다 (상대는 잡을 수 있다)
        if (context.IsMine(this)) return;
        // 상대 이동이면 잡힐 목록에서 내 말을 뺀다
        captured.RemoveAll(piece => AugmentContext.IsOwnedBy(this, piece));
    }
}
