using System.Collections.Generic;

public class BoardGraph
{
    private readonly Dictionary<int, BoardNode> nodes;

    // 판 밖 출발 칸. 앞으로 가다가 이 칸에 들어오면 골인
    public BoardNode StartNode { get; }

    // 모서리/방에 멈췄을 때 지름길 말고 원래 길도 고를 수 있게 할지
    public bool AllowSkipShortcut { get; set; }

    public IReadOnlyCollection<BoardNode> Nodes => nodes.Values;

    public BoardGraph(Dictionary<int, BoardNode> nodes, BoardNode startNode)
    {
        this.nodes = nodes;
        StartNode = startNode;
    }

    // 저장/네트워크로 받은 칸 번호로 칸을 찾는다. 없으면 null
    public BoardNode GetNode(int id) => nodes.TryGetValue(id, out BoardNode node) ? node : null;

    // start: 말의 현재 칸 (판에 안 나온 말은 StartNode)
    // cameFrom: 말이 직전에 있던 칸 (판에 안 나온 말은 null)
    // moveCount: 도=1 ~ 모=5, 빽도=-1 (증강으로 -4처럼 여러 칸 뒤로 가는 것도 가능)
    public List<BoardPath> GetMovePaths(BoardNode start, BoardNode cameFrom, int moveCount)
    {
        var result = new List<BoardPath>();
        if (start == null || moveCount == 0) return result;

        if (moveCount < 0)
        {
            BoardPath back = MoveBackward(start, cameFrom, -moveCount);
            if (back != null) result.Add(back);
            return result;
        }

        // 첫 걸음 후보: 멈춘 칸의 지름길 (+ 옵션에 따라 원래 길)
        var firstSteps = new List<BoardNode>(start.ShortcutNodes);
        if (firstSteps.Count == 0 || AllowSkipShortcut)
        {
            BoardNode normal = start.GetNext(cameFrom);
            if (normal != null && !firstSteps.Contains(normal)) firstSteps.Add(normal);
        }

        foreach (BoardNode first in firstSteps)
        {
            result.Add(MoveForward(start, cameFrom, first, moveCount));
        }

        return result;
    }

    private BoardPath MoveForward(BoardNode start, BoardNode cameFrom, BoardNode first, int moveCount)
    {
        var path = new List<BoardNode>();
        BoardNode prev = cameFrom;
        BoardNode current = start;
        BoardNode next = first;

        for (int i = 0; i < moveCount && next != null; i++)
        {
            prev = current;
            current = next;
            path.Add(current);

            // End를 지나 Start로 들어오면 남은 칸과 상관없이 골인
            if (current == StartNode) return new BoardPath(start, path, true, false, prev);

            next = current.GetNext(prev);
        }

        return new BoardPath(start, path, false, false, prev);
    }

    private BoardPath MoveBackward(BoardNode start, BoardNode cameFrom, int moveCount)
    {
        // 판에 안 나온 말은 뒤로 움직일 수 없음
        if (start == StartNode) return null;

        var path = new List<BoardNode>();
        BoardNode ahead = null; // 뒤로 가기 직전에 있던 칸 (앞쪽 칸)
        BoardNode current = start;

        // 첫 걸음: 직전 칸이 이 칸의 Prev에 있으면 왔던 길로 되돌아간다 (Start에서 막 나온 말은 Prev[0] = End로)
        BoardNode back = cameFrom != null && current.PrevNodes.Contains(cameFrom) ? cameFrom : GetBack(current, null);

        for (int i = 0; i < moveCount && back != null; i++)
        {
            ahead = current;
            current = back;
            path.Add(current);
            back = GetBack(current, ahead);
        }

        if (path.Count == 0) return null;

        // 도착 칸에서 다음에 앞으로 갈 때 쓸 직전 칸 = 한 칸 더 뒤로 갔다면 갔을 칸
        BoardNode backCameFrom = current == StartNode ? null : back;
        return new BoardPath(start, path, false, true, backCameFrom);
    }

    // 뒤로 한 칸. Next[j] 쪽에서 뒤로 들어왔으면 Prev[j]로 나간다 (앞으로 갈 때 짝의 역방향). 짝이 없으면 Prev[0]
    private static BoardNode GetBack(BoardNode node, BoardNode ahead)
    {
        if (node.PrevNodes.Count == 0) return null;

        int index = ahead != null ? node.NextNodes.IndexOf(ahead) : -1;
        return index >= 0 && index < node.PrevNodes.Count ? node.PrevNodes[index] : node.PrevNodes[0];
    }
}
