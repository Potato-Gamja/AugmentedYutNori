using System;
using System.Collections.Generic;

public class BoardGraph
{
    private readonly Dictionary<int, BoardNode> nodes = new Dictionary<int, BoardNode>();

    // 판 밖 출발 칸. 앞으로 가다가 이 칸에 들어오면 골인
    public BoardNode StartNode { get; private set; }

    // 모서리/방에 멈췄을 때 지름길 말고 원래 길도 고를 수 있게 할지
    public bool AllowSkipShortcut { get; set; }

    public IReadOnlyCollection<BoardNode> Nodes => nodes.Values;

    // 빈 그래프. 판 모양은 Rebuild(BoardDefinition)로 채운다 (보통 BoardDefinition.CreateGraph() 사용)
    public BoardGraph() { }

    // 저장/네트워크로 받은 칸 번호로 칸을 찾는다. 없으면 null
    public BoardNode GetNode(int id) => nodes.TryGetValue(id, out BoardNode node) ? node : null;

    // 이 그래프에 들어 있는 칸인지 (판이 바뀌어 빠진 칸이면 false)
    public bool Contains(BoardNode node) => node != null && GetNode(node.Id) == node;

    // Rebuild로 판 모양이 바뀐 뒤. BoardState가 구독해 없어진 칸의 말을 정리한다
    public event Action Rebuilt;

    // 판 모양을 제자리에서 바꾼다. 같은 번호의 BoardNode 객체는 그대로 두고 종류/연결만 바꾸므로,
    // 그 칸을 가리키는 말(Piece)과 BoardState/GameState는 다시 만들 필요 없이 같은 번호의 칸에 그대로 남는다.
    // 새 판에 없는 번호의 칸은 빠진다 (그 칸에 있던 말은 갈 곳이 없어지므로, 번호가 같은 판끼리 바꿀 것)
    public void Rebuild(BoardDefinition definition)
    {
        if (!definition.Validate(out string error)) throw new InvalidOperationException($"[BoardGraph] {error}");

        var ids = new HashSet<int>();
        foreach (NodeDefinition def in definition.nodes)
        {
            ids.Add(def.id);
            if (nodes.TryGetValue(def.id, out BoardNode node)) node.Type = def.type;
            else nodes.Add(def.id, new BoardNode(def.id, def.type));
        }

        var removed = new List<int>();
        foreach (int id in nodes.Keys) if (!ids.Contains(id)) removed.Add(id);
        foreach (int id in removed) nodes.Remove(id);

        foreach (NodeDefinition def in definition.nodes)
        {
            BoardNode node = nodes[def.id];
            node.NextNodes.Clear();
            node.PrevNodes.Clear();
            node.ShortcutNodes.Clear();
            foreach (int id in def.next) node.NextNodes.Add(nodes[id]);
            foreach (int id in def.prev) node.PrevNodes.Add(nodes[id]);
            foreach (int id in def.shortcut) node.ShortcutNodes.Add(nodes[id]);
        }

        StartNode = nodes[definition.startNodeId];
        Rebuilt?.Invoke();
    }

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

    // 지름길을 무시하고 원래 길(바깥 길)로만 가는 경로.
    // 갈 곳이 없으면 null (예: 마라토너 증강)
    public BoardPath GetNormalPath(BoardNode start, BoardNode cameFrom, int moveCount)
    {
        if (start == null || moveCount <= 0) return null;
        BoardNode normal = start.GetNext(cameFrom);
        return normal == null ? null : MoveForward(start, cameFrom, normal, moveCount);
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
