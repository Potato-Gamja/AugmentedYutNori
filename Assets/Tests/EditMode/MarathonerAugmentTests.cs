// 마라토너(Marathoner): 앞으로 갈 때 한 칸 더 가고, 지름길로는 갈 수 없는지 검증하는 테스트
using NUnit.Framework;

public class MarathonerAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    // 지름길이 하나 있는 작은 판
    //   Start(0) → 1 → 2 → 3(모서리) → 4 → 5 → 6 → End(7) → Start(0) 골인
    //                         ↘ 지름길 10 → 11 → End(7)
    // 3번 칸에 멈춘 말은 지름길(10)로만 갈 수 있다 (AllowSkipShortcut이 꺼져 있으면)
    private static BoardState MakeShortcutBoard()
    {
        var definition = new BoardDefinition { startNodeId = 0 };
        definition.nodes.Add(Node(0, BoardNodeType.Start, next: new[] { 1 }));
        definition.nodes.Add(Node(1, BoardNodeType.Normal, next: new[] { 2 }, prev: new[] { 7 }));
        definition.nodes.Add(Node(2, BoardNodeType.Normal, next: new[] { 3 }, prev: new[] { 1 }));
        definition.nodes.Add(Node(3, BoardNodeType.Corner, next: new[] { 4 }, prev: new[] { 2 }, shortcut: new[] { 10 }));
        definition.nodes.Add(Node(4, BoardNodeType.Normal, next: new[] { 5 }, prev: new[] { 3 }));
        definition.nodes.Add(Node(5, BoardNodeType.Normal, next: new[] { 6 }, prev: new[] { 4 }));
        definition.nodes.Add(Node(6, BoardNodeType.Normal, next: new[] { 7 }, prev: new[] { 5 }));
        definition.nodes.Add(Node(7, BoardNodeType.End, next: new[] { 0 }, prev: new[] { 6, 11 }));
        definition.nodes.Add(Node(10, BoardNodeType.Normal, next: new[] { 11 }, prev: new[] { 3 }));
        definition.nodes.Add(Node(11, BoardNodeType.Normal, next: new[] { 7 }, prev: new[] { 10 }));
        return new BoardState(definition.CreateGraph());
    }

    private static NodeDefinition Node(int id, BoardNodeType type, int[] next, int[] prev = null, int[] shortcut = null)
    {
        var node = new NodeDefinition { id = id, type = type };
        node.next.AddRange(next);
        if (prev != null) node.prev.AddRange(prev);
        if (shortcut != null) node.shortcut.AddRange(shortcut);
        return node;
    }

    // ───── 한 칸 더 ─────

    [Test]
    public void Forward_OneMoreStep()
    {
        // 준비: 대기 중인 내 말
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new MarathonerAugment(MyTeam));

        // 실행 + 검증: 개(2칸) → 3칸 가서 3번 칸
        Assert.IsFalse(board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(2), out _));
        Assert.IsTrue(board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out _));
    }

    [Test]
    public void BackDo_Unchanged()
    {
        // 준비: 내 말 3번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.AddAugment(new MarathonerAugment(MyTeam));

        // 실행 + 검증: 빽도는 그대로 뒤로 1칸
        Assert.AreEqual(-1, board.State.GetModifiedMoveCount(myPiece, YutResult.BackDo.Steps()));
        Assert.IsTrue(board.State.TryMove(myPiece, YutResult.BackDo.Steps(), board.Node(2), out _));
    }

    [Test]
    public void OtherTeam_NoExtraStep()
    {
        // 준비: 상대 말 (증강은 내 팀만)
        var board = new TestBoard();
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.AddAugment(new MarathonerAugment(MyTeam));

        // 실행 + 검증: 개는 그대로 2칸
        Assert.IsTrue(board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(2), out _));
    }

    // ───── 지름길 불가 ─────

    [Test]
    public void StoppedAtCorner_NoShortcut_UsesNormalRoad()
    {
        // 준비: 내 말이 모서리(3번)에 멈춰 있음. 원래 길 선택 옵션은 꺼짐 → 원래대로면 지름길만 가능
        BoardState state = MakeShortcutBoard();
        Piece myPiece = state.AddPiece(0, MyTeam);
        state.TryMove(myPiece, 3, state.Graph.GetNode(3), out _);
        state.AddAugment(new MarathonerAugment(MyTeam));

        // 실행
        var paths = state.GetMovePaths(myPiece, YutResult.Do.Steps());

        // 검증: 지름길 경로는 없고, 원래 길로 2칸(도 + 1) 가는 경로 하나만 있다 → 모서리에서도 움직일 수 있다
        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual(state.Graph.GetNode(5), paths[0].Destination);
        Assert.IsFalse(state.TryMove(myPiece, YutResult.Do.Steps(), state.Graph.GetNode(10), out _));
        Assert.IsTrue(state.TryMove(myPiece, YutResult.Do.Steps(), state.Graph.GetNode(5), out _));
    }

    [Test]
    public void StoppedAtCorner_AllowSkip_NormalRoadOnce()
    {
        // 준비: 원래 길 선택 옵션이 켜진 판 → 원래대로면 지름길과 원래 길 둘 다 가능
        BoardState state = MakeShortcutBoard();
        state.Graph.AllowSkipShortcut = true;
        Piece myPiece = state.AddPiece(0, MyTeam);
        state.TryMove(myPiece, 3, state.Graph.GetNode(3), out _);
        state.AddAugment(new MarathonerAugment(MyTeam));

        // 실행
        var paths = state.GetMovePaths(myPiece, YutResult.Do.Steps());

        // 검증: 지름길만 빠지고 원래 길은 중복 없이 하나
        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual(state.Graph.GetNode(5), paths[0].Destination);
    }

    [Test]
    public void PassingCorner_GoesStraight()
    {
        // 준비: 내 말 2번 칸 (모서리 바로 앞)
        BoardState state = MakeShortcutBoard();
        Piece myPiece = state.AddPiece(0, MyTeam);
        state.TryMove(myPiece, 2, state.Graph.GetNode(2), out _);
        state.AddAugment(new MarathonerAugment(MyTeam));

        // 실행 + 검증: 개(2칸) + 1 = 3칸, 모서리를 지나쳐 3 → 4 → 5
        Assert.IsTrue(state.TryMove(myPiece, YutResult.Gae.Steps(), state.Graph.GetNode(5), out _));
    }

    [Test]
    public void OtherTeam_CanUseShortcut()
    {
        // 준비: 상대 말이 모서리(3번)에 멈춰 있음 (증강은 내 팀만)
        BoardState state = MakeShortcutBoard();
        Piece otherPiece = state.AddPiece(1, OtherTeam);
        state.TryMove(otherPiece, 3, state.Graph.GetNode(3), out _);
        state.AddAugment(new MarathonerAugment(MyTeam));

        // 실행 + 검증: 상대 말은 평소처럼 지름길로 간다
        Assert.IsTrue(state.TryMove(otherPiece, YutResult.Do.Steps(), state.Graph.GetNode(10), out _));
    }
}