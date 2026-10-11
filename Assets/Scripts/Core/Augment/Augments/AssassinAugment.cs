// 도착 칸이 아니라 한 칸 앞(갈림길이면 다음 칸 전부)의 상대 말을 잡는 증강
using System.Collections.Generic;

public class AssassinAugment : Augment
{
    public override string Id => "Assassin";

    // 평화주의자(0)보다 나중, 프렌드쉴드(20)보다 먼저 실행한다
    // → 평화주의자 팀도 잡을 수 있고, 프렌드쉴드 팀의 업힌 말은 한 마리만 잡힌다
    public override int Priority => 10;

    public AssassinAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyCaptures(AugmentContext context, List<Piece> captured)
    {
        if (!context.IsMine(this)) return;

        // 도착 칸의 일반 잡기는 할 수 없다 (목록 비우기)
        captured.Clear();

        // 현재 말의 도착 칸
        BoardNode destination = context.Piece.Node;

        // 한 칸 앞 칸들: 도착 칸의 지름길 칸들 + 바깥 길 다음 칸
        var targets = new List<BoardNode>(destination.ShortcutNodes);
        BoardNode next = destination.GetNext(context.Piece.CameFrom);
        if (next != null && !targets.Contains(next)) targets.Add(next);

        // 해당 칸에 있는 다른 팀 말을 모두 잡을 목록에 추가
        foreach (BoardNode node in targets)
        {
            foreach (Piece piece in context.State.GetPiecesAt(node))
            {
                if (piece.Team != context.Team) captured.Add(piece);
            }
        }
    }
}
