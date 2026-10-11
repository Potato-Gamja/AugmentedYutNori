// 지름길로 갈 수 없는 대신 앞으로 갈 때 한 칸 더 가는 증강
using System.Collections.Generic;

public class MarathonerAugment : Augment
{
    public override string Id => "Marathoner";

    public MarathonerAugment(int ownerTeam) : base(ownerTeam) { }

    public override int ModifyMoveCount(AugmentContext context, int moveCount)
    {
        if (!context.IsMine(this)) return moveCount;
        if (moveCount <= 0) return moveCount;
        return moveCount + 1;
    }

    public override void ModifyPaths(AugmentContext context, List<BoardPath> paths)
    {
        if (!context.IsMine(this)) return;

        // 지름길인 경로 제거
        BoardNode start = context.Piece.Node;
        paths.RemoveAll(path => path.Nodes.Count > 0 && start.ShortcutNodes.Contains(path.Nodes[0]));

        // 원래 길 경로가 없으면 추가 (지름길만 있던 모서리에 멈춘 말도 움직일 수 있게)
        BoardNode normalFirst = start.GetNext(context.Piece.CameFrom);
        bool hasNormal = paths.Exists(path => path.Nodes.Count > 0 && path.Nodes[0] == normalFirst);
        if (!hasNormal && context.MoveCount > 0)
        {
            BoardPath normal = context.Graph.GetNormalPath(start, context.Piece.CameFrom, context.MoveCount);
            if (normal != null) paths.Add(normal);
        }
    }
}
