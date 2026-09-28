using System.Collections.Generic;

// 한 번의 이동 결과. 뷰는 Nodes로 경로를 표시하고, 말은 Destination/CameFrom으로 위치를 갱신한다.
public class BoardPath
{
    public BoardNode Start { get; }
    public IReadOnlyList<BoardNode> Nodes { get; } // 지나가는 칸 순서 (시작 칸 제외, 마지막이 도착 칸)
    public bool IsFinished { get; }                // End(참먹이)를 지나 Start로 들어와 골인
    public bool IsBackward { get; }                // 빽도

    // 도착 후 말이 기억할 직전 칸. 빽도로 갈림길 칸에 도착하면 알 수 없어서 null
    public BoardNode CameFrom { get; }

    public BoardNode Destination => Nodes.Count > 0 ? Nodes[Nodes.Count - 1] : Start;

    public BoardPath(BoardNode start, List<BoardNode> nodes, bool isFinished, bool isBackward, BoardNode cameFrom)
    {
        Start = start;
        Nodes = nodes;
        IsFinished = isFinished;
        IsBackward = isBackward;
        CameFrom = cameFrom;
    }

    public override string ToString() =>
        $"{Start} -> [{string.Join(", ", Nodes)}]{(IsFinished ? " 골인" : "")}{(IsBackward ? " 빽도" : "")}";
}
