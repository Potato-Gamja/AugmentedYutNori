// 암살자(Assassin): 도착 칸의 말은 못 잡고, 한 칸 앞(갈림길이면 다음 칸 전부)의 상대 말을 잡는지 검증하는 테스트
using System.Linq;
using NUnit.Framework;

public class AssassinAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    // 지름길이 하나 있는 작은 판 (MarathonerAugmentTests와 같은 모양)
    //   Start(0) → 1 → 2 → 3(모서리) → 4 → 5 → 6 → End(7) → Start(0) 골인
    //                         ↘ 지름길 10 → 11 → End(7)
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

    // ───── 기본 규칙 ─────

    [Test]
    public void EnemyAtDestination_NotCaptured()
    {
        // 준비: 상대 말 3번 칸, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 3, board.Node(3), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행: 내 말이 개로 상대 말 칸(3번)에 도착
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 도착 칸의 일반 잡기는 할 수 없다 → 같은 칸에 함께 남는다
        Assert.IsEmpty(result.CapturedPieces);
        Assert.AreEqual(board.Node(3), otherPiece.Node);
    }

    [Test]
    public void EnemyOneAhead_Captured()
    {
        // 준비: 상대 말 4번 칸, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 4, board.Node(4), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행: 내 말이 3번 칸에 도착
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 한 칸 앞(4번)의 상대 말이 잡힌다
        CollectionAssert.AreEquivalent(new[] { otherPiece }, result.CapturedPieces);
        Assert.IsTrue(otherPiece.IsWaiting);
    }

    [Test]
    public void StackedEnemiesAhead_AllCaptured()
    {
        // 준비: 상대 말 두 개를 4번 칸에 업기, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherA = board.State.AddPiece(1, OtherTeam);
        Piece otherB = board.State.AddPiece(2, OtherTeam);
        board.State.TryMove(otherA, 4, board.Node(4), out _);
        board.State.TryMove(otherB, 4, board.Node(4), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 업힌 말은 전부 잡힌다
        CollectionAssert.AreEquivalent(new[] { otherA, otherB }, result.CapturedPieces);
    }

    [Test]
    public void MyPieceAhead_NotCaptured()
    {
        // 준비: 내 말 B 4번 칸, 내 말 A 1번 칸
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        board.State.TryMove(myB, 4, board.Node(4), out _);
        board.State.TryMove(myA, 1, board.Node(1), out _);
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행
        board.State.TryMove(myA, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 내 말은 잡지 않는다
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(myB.IsOnBoard);
    }

    [Test]
    public void OtherTeamMove_CapturesNormally()
    {
        // 준비: 내 말 3번 칸(암살자 보유), 상대 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행: 상대 말이 내 말 칸에 도착
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 암살자 규칙은 내가 움직일 때만 → 상대는 평소처럼 잡는다
        CollectionAssert.AreEquivalent(new[] { myPiece }, result.CapturedPieces);
    }

    [Test]
    public void ArriveAtCorner_CapturesBothBranches()
    {
        // 준비: 상대 말 A 4번 칸(바깥 길), 상대 말 B 10번 칸(지름길), 내 말 2번 칸
        BoardState state = MakeShortcutBoard();
        Piece myPiece = state.AddPiece(0, MyTeam);
        Piece otherA = state.AddPiece(1, OtherTeam);
        Piece otherB = state.AddPiece(2, OtherTeam);
        state.TryMove(otherA, 4, state.Graph.GetNode(4), out _);
        state.TryMove(otherB, 3, state.Graph.GetNode(3), out _);
        state.TryMove(otherB, 1, state.Graph.GetNode(10), out _);
        state.TryMove(myPiece, 2, state.Graph.GetNode(2), out _);
        state.AddAugment(new AssassinAugment(MyTeam));

        // 실행: 내 말이 도로 모서리(3번)에 도착
        state.TryMove(myPiece, YutResult.Do.Steps(), state.Graph.GetNode(3), out MoveResult result);

        // 검증: 갈림길이면 다음 칸 전부 → 바깥 칸과 지름길 칸의 상대 말 둘 다 잡힌다
        CollectionAssert.AreEquivalent(new[] { otherA, otherB }, result.CapturedPieces);
    }

    // ───── 방어 증강과의 순서 (Priority) ─────

    [Test]
    public void PacifistTeam_Captured()
    {
        // 준비: 상대 말 4번 칸, 내 말 1번 칸. 평화주의자를 먼저 등록해도 암살이 우선
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 4, board.Node(4), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(OtherTeam));
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 평화주의자 팀도 암살자에게는 잡힌다
        CollectionAssert.AreEquivalent(new[] { otherPiece }, result.CapturedPieces);
    }

    [Test]
    public void FriendShieldTeam_OnlyOneCaptured()
    {
        // 준비: 상대 말 두 개를 4번 칸에 업기, 내 말 1번 칸. 프렌드쉴드를 먼저 등록
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherA = board.State.AddPiece(1, OtherTeam);
        Piece otherB = board.State.AddPiece(2, OtherTeam);
        board.State.TryMove(otherA, 4, board.Node(4), out _);
        board.State.TryMove(otherB, 4, board.Node(4), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new FriendShieldAugment(OtherTeam));
        board.State.AddAugment(new AssassinAugment(MyTeam));

        // 실행
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 프렌드쉴드가 암살자 뒤에 실행되어 업힌 말 중 한 마리만 잡힌다
        Piece[] others = { otherA, otherB };
        Assert.AreEqual(1, result.CapturedPieces.Count);
        Assert.AreEqual(1, others.Count(p => p.IsOnBoard));
        Assert.AreEqual(board.Node(4), others.First(p => p.IsOnBoard).Node);
    }
}