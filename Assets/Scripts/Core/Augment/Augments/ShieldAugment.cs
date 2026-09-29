using System.Collections.Generic;

// 내 말이 처음 잡힐 때 한 번 무효화
public class ShieldAugment : Augment
{
    public override string Id => "Shield";

    public ShieldAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyCaptures(AugmentContext context, List<Piece> captured)
    {
        // 잡힌 말이 이 증강을 소유하고 있다면 무효화 (업힌 말도 한 번에 지킵니다)
        int saved = captured.RemoveAll(piece => AugmentContext.IsOwnedBy(this, piece));
        // 만약 무효화했다면 1회용 이므로 증강 제거
        if (saved > 0) context.State.RemoveAugment(this);
    }
}