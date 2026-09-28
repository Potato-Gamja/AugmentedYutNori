// 새 말을 판에 올릴 때 이동 칸 수 +1
public class QuickStartAugment : Augment
{
    public override string Id => "QuickStart";

    public QuickStartAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        // 내 말이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this)) return moveCount;
        // 새 말을 판에 올리는 경우가 아니면 아무것도 하지 않는다
        if (!context.IsWaiting) return moveCount;
        // 빽도로 출발하는 경우 아무것도 하지 않는다
        if (moveCount <= 0) return moveCount;
        // 이동 칸 수 +1
        return moveCount + 1;
    }
}