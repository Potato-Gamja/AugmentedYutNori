using System.Collections.Generic;

// 빽도가 나오면 뒤로 1칸 대신 앞으로 1칸(도)으로 가는 경로도 고를 수 있게 하는 증강
public class BackDoSwitchAugment : Augment
{
    public override string Id => "BackDoSwitch";

    public BackDoSwitchAugment(int ownerTeam) : base(ownerTeam) { }

    public override void ModifyPaths(AugmentContext context, List<BoardPath> paths)
    {
        // 빽도가 나오지 않았거나, 내 턴이 아니면 아무것도 하지 않는다
        if (!context.IsMine(this) || context.RolledMoveCount != YutResult.BackDo.Steps()) return;

        // 앞으로 1칸(도)으로 가는 경로를 추가한다
        paths.AddRange(context.Graph.GetMovePaths(
            context.Piece.Node, context.Piece.CameFrom, YutResult.Do.Steps()));
    }
}