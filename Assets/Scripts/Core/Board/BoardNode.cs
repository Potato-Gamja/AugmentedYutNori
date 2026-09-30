using System.Collections.Generic;

public enum BoardNodeType
{
    Normal,
    Start,  // 판 밖 출발 칸. 판에 안 나온 말이 있는 곳이자, End를 지나 여기로 들어오면 골인
    End,    // 참먹이. 여기를 지나가야 골인
    Corner, // 모, 뒷모, 찌모
    Center, // 방
}

public class BoardNode
{
    public int Id { get; } // 고정 번호. 저장/네트워크/증강 효과에서 칸을 가리킬 때 사용
    public BoardNodeType Type { get; internal set; } // 판을 제자리에서 바꿀 때(BoardGraph.Rebuild)만 변경

    // PrevNodes[i]에서 들어온 말은 NextNodes[i]로 나간다. 짝이 없으면 NextNodes[0]
    public List<BoardNode> NextNodes { get; } = new List<BoardNode>();
    public List<BoardNode> PrevNodes { get; } = new List<BoardNode>();
    public List<BoardNode> ShortcutNodes { get; } = new List<BoardNode>(); // 해당 지역에 멈췄을 때만 움직임

    public BoardNode(int id, BoardNodeType type)
    {
        Id = id;
        Type = type;
    }

    // 들어온 방향을 기준으로 다음 칸을 고른다. 갈 곳이 없으면 null
    public BoardNode GetNext(BoardNode cameFrom)
    {
        if (NextNodes.Count == 0) return null;

        int index = cameFrom != null ? PrevNodes.IndexOf(cameFrom) : -1;
        return index >= 0 && index < NextNodes.Count ? NextNodes[index] : NextNodes[0];
    }

    public override string ToString() => $"Node{Id}({Type})";
}
