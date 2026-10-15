// 노장: 말이 하나 적은 대신 윷·모일 때 한 칸 덜 간다 (나머지는 그대로). 말 수는 GameFlowController가 처리
public class VeteranAugment : Augment
{
    public override string Id => "Veteran";

    public VeteranAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        // 내 말이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this)) return moveCount;

        int rolled = context.RolledMoveCount;
        // 윷·모만 1칸 덜 (윷 3, 모 4). 빽도·도·개·걸은 그대로
        if (rolled == YutResult.Yut.Steps() || rolled == YutResult.Mo.Steps()) return moveCount - 1;
        return moveCount;
    }

    // 팀의 말 수: 기본 말 수에서 노장이면 1개 적게 (서버도 같은 규칙)
    public static int PieceCountFor(BoardState board, int team, int basePieces)
        => board.GetAugments(team).Exists(a => a is VeteranAugment) ? basePieces - 1 : basePieces;
}
