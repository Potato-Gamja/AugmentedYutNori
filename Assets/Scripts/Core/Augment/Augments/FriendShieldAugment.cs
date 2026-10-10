using System.Collections.Generic;

// 업힌 내 말이 잡혀도 한 마리만 대기석으로 돌아가게 하는 증강
public class FriendShieldAugment : Augment
{
    public override string Id => "FriendShield";

    // 암살자(10)가 잡을 말을 넣은 뒤에도 업힌 말 중 한 마리만 잡히도록 마지막에 실행한다
    public override int Priority => 20;

    public FriendShieldAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyCaptures(AugmentContext context, List<Piece> captured)
    {
        // 잡힐 말 중 이 증강을 가지고 있는 플레이어의 말을 찾습니다
        List<Piece> pieces = captured.FindAll(piece => AugmentContext.IsOwnedBy(this, piece));

        // 업고 있는 말이 아니라면 아무것도 하지 않습니다
        if (pieces.Count < 2) return;

        // 업고 있는 말 중 마지막 말만 잡히도록 합니다
        for (int i = 0; i < pieces.Count - 1; i++)
        {
            captured.Remove(pieces[i]);
        }
    }
}