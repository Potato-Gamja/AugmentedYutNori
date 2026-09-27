using NUnit.Framework;
using System;
using System.Collections.Generic;


[Flags]
public enum NodeType
{
    Normal = 0, // 0000
    Start = 1 << 0, // 0001
    Corner = 1 << 1, // 0010
    Center = 1 << 2, // 0100
    Finish = 1 << 3, // 1000
}

public class BoardNode
{
    private readonly List<BoardNode> _previous = new List<BoardNode>();

    public int Id { get; }
    public NodeType Type { get; }

    public BoardNode Next { get; private set; } // 다음 노드
    public BoardNode Shortcut { get; private set; } // 멈췄을 때 들어갈 수 있는 지름길 노드
    public IReadOnlyList<BoardNode> Previous => _previous; // 빽도 계산용

    public bool IsStart => Has(NodeType.Start);
    public bool IsCorner => Has(NodeType.Corner);
    public bool IsCenter => Has(NodeType.Center);
    public bool IsFinish => Has(NodeType.Finish);
    public bool HasShortcut => Shortcut != null;

    public BoardNode(int id, NodeType type = NodeType.Normal)
    {
        if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "노드 id는 0 이상");
        Id = id;
        Type = type;
    }

    public bool Has(NodeType flag) => (Type & flag) == flag;

    // 기본 진행 방향 연결
    public void SetNext(BoardNode node)
    {
        Link(Next, node);
        Next = node;
    }

    // 지름길 연결.
    public void SetShortcut(BoardNode node)
    {
        Link(Shortcut, node);
        Shortcut = node;
    }

    // 이동 시작 시 처음으로 거쳐가는 노드.
    // 이동 시작 칸이 지름길 칸이면 지름길로, 아니면 기본 방향
    public BoardNode GetDepartureNode(bool useShortcut = true)
    {
        return useShortcut && HasShortcut ? Shortcut : Next;
    }

    // 다음 칸을 지정해줄 때, 대상 노드의 Previous 갱신
    private void Link(BoardNode old, BoardNode target)
    {
        if (target == this) throw new InvalidOperationException($"노드 {Id}가 자기 자신을 가리킬 수 없습니다.");

        old?._previous.Remove(this);

        if (target != null && !target._previous.Contains(this)) target._previous.Add(this);
    }

    public override string ToString()
    {
        string next = Next != null ? Next.Id.ToString() : "-";
        string sc = Shortcut != null ? Shortcut.Id.ToString() : "-";
        return $"BoardNode({Id}, {Type}, next: {next}, shortcut: {sc})";
    }
}
