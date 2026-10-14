// 노장: 말이 하나 적은 대신 한 칸 덜 간다 (도는 그대로, 빽도는 움직이지 않음). 말 수는 GameFlowController가 처리
public class VeteranAugment : Augment
{
    public override string Id => "Veteran";

    public VeteranAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        // 내 말이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this)) return moveCount;

        int rolled = context.RolledMoveCount;
        // 빽도는 움직이지 않는다 (0칸 → 이동 경로가 없어 쓸 수 없는 결과)
        if (rolled == YutResult.BackDo.Steps()) return 0;
        // 도는 그대로
        if (rolled == YutResult.Do.Steps()) return moveCount;
        // 개~모는 1칸 덜
        return moveCount - 1;
    }
}
