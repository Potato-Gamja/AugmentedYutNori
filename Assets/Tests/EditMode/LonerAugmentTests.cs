// 외톨이(Loner): 판 위에 내 말을 하나만 둘 수 있고(업기 불가), 상대에게 잡히지 않는지 검증하는 테스트
using NUnit.Framework;

public class LonerAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    // ───── 판에 한 개만 ─────

    [Test]
    public void PieceOnBoard_WaitingCannotStart()
    {
        // 준비: 내 말 A 3번 칸, 내 말 B 대기 중
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행 + 검증: 판에 내 말이 있으면 대기 말은 출발할 수 없다
        Assert.IsEmpty(board.State.GetMovePaths(myB, YutResult.Gae.Steps()));
        Assert.IsFalse(board.State.TryMove(myB, YutResult.Gae.Steps(), board.Node(2), out _));
        Assert.IsTrue(myB.IsWaiting);
    }

    [Test]
    public void NoPieceOnBoard_WaitingCanStart()
    {
        // 준비: 내 말 둘 다 대기 중
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행: A 출발
        bool started = board.State.TryMove(myA, YutResult.Gae.Steps(), board.Node(2), out _);

        // 검증: 판이 비어 있으면 출발할 수 있고, 그 뒤에는 B가 막힌다
        Assert.IsTrue(started);
        Assert.IsEmpty(board.State.GetMovePaths(myB, YutResult.Gae.Steps()));
    }

    [Test]
    public void OnBoardPiece_MovesNormally()
    {
        // 준비: 내 말 3번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행 + 검증: 판 위의 말은 평소처럼 움직인다
        Assert.IsTrue(board.State.TryMove(myPiece, YutResult.Do.Steps(), board.Node(4), out _));
    }

    [Test]
    public void AfterFinish_NextCanStart()
    {
        // 준비: 내 말 A 9번 칸(골인 직전), 내 말 B 대기 중
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        board.State.TryMove(myA, 9, board.Node(9), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행: A가 개로 End(10)를 지나 골인
        board.State.TryMove(myA, YutResult.Gae.Steps(), board.Node(0), out _);

        // 검증: 판 위에 내 말이 없어졌으니 B가 출발할 수 있다
        Assert.IsTrue(myA.IsFinished);
        Assert.IsNotEmpty(board.State.GetMovePaths(myB, YutResult.Do.Steps()));
    }

    [Test]
    public void OtherTeamWaiting_Unaffected()
    {
        // 준비: 상대 말 A 3번 칸, 상대 말 B 대기 중 (외톨이는 내 팀만)
        var board = new TestBoard();
        Piece otherA = board.State.AddPiece(1, OtherTeam);
        Piece otherB = board.State.AddPiece(3, OtherTeam);
        board.State.TryMove(otherA, 3, board.Node(3), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행 + 검증: 상대 팀은 평소처럼 여러 말을 판에 올릴 수 있다
        Assert.IsTrue(board.State.TryMove(otherB, YutResult.Gae.Steps(), board.Node(2), out _));
    }

    // ───── 잡기 ─────

    [Test]
    public void OtherLandsOnMine_NotCaptured()
    {
        // 준비: 내 말 3번 칸, 상대 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 내 말은 잡히지 않는다
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(myPiece.IsOnBoard);
    }

    [Test]
    public void MyLandsOnOther_Captures()
    {
        // 준비: 상대 말 3번 칸, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 3, board.Node(3), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));

        // 실행
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 평화주의자와 달리 상대는 잡을 수 있다
        CollectionAssert.AreEquivalent(new[] { otherPiece }, result.CapturedPieces);
    }

    [Test]
    public void AssassinCapturesLoner()
    {
        // 준비: 내 말 4번 칸(외톨이), 상대 말 1번 칸(암살자)
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 4, board.Node(4), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new LonerAugment(MyTeam));
        board.State.AddAugment(new AssassinAugment(OtherTeam));

        // 실행: 상대 말이 3번 칸에 도착 → 한 칸 앞(4번)이 내 말
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 외톨이도 암살자에게는 잡힌다 (평화주의자와 같은 취급)
        CollectionAssert.AreEquivalent(new[] { myPiece }, result.CapturedPieces);
    }
}