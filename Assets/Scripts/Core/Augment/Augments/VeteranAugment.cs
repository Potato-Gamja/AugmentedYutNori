// 노장: 말이 하나 적고 윷·모만 한 칸 덜 간다. 말 수는 GameFlowController가 처리
public class VeteranAugment : Augment
{
    public override string Id => "Veteran";

    public VeteranAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        // 내 말이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this)) return moveCount;

        int rolled = context.RolledMoveCount;
        bool far = rolled == YutResult.Yut.Steps() || rolled == YutResult.Mo.Steps();
        // 서버와 동일하게 원래 윷·모였을 때만 보정한다. 빽도·도·개·걸은 그대로 둔다.
        return far ? moveCount - 1 : moveCount;
    }

    // 팀의 말 수: 기본 말 수에서 노장이면 1개 적게 (서버도 같은 규칙)
    public static int PieceCountFor(BoardState board, int team, int basePieces)
        => board.GetAugments(team).Exists(a => a is VeteranAugment) ? basePieces - 1 : basePieces;
}
