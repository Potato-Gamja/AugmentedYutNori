using System.Collections.Generic;

// 잡히지 않는 대신 상대 말을 잡을 수 없게 하는 증강
public class PacifistAugment : Augment
{
    public override string Id => "Pacifist";

    public PacifistAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyCaptures(AugmentContext context, List<Piece> captured)
    {
        // 내 말이 움직이는 이동이라면 상대 말을 잡지 못하도록 모두 제거합니다
        if (context.IsMine(this))
        {
            captured.Clear();
            return;
        }

        // 상대 이동 중에 내 말이 잡히지 않도록 내 말을 잡힐 목록에서 제거합니다
        captured.RemoveAll(piece => AugmentContext.IsOwnedBy(this, piece));
    }
}