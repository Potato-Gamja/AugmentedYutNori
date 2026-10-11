// 도·개가 나오면 1칸 더 이동하는 증강
public class MonkAugment : Augment
{
    public override string Id => "Monk";

    public MonkAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        // 내 말이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this)) return moveCount;
        // 도·개가 아니면 아무것도 하지 않는다
        int rolled = context.RolledMoveCount;
        if (rolled != YutResult.Do.Steps() && rolled != YutResult.Gae.Steps()) return moveCount;

        // 1칸 더 추가
        return moveCount + 1;
    }
}