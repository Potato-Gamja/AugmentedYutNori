// 평화주의자(Pacifist): 내 말은 잡히지 않고, 대신 내 말도 상대 말을 잡을 수 없는지 검증하는 테스트
using NUnit.Framework;

public class PacifistAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;
    private const int ThirdTeam = 2;

    [Test]
    public void OtherLandsOnMine_NotCaptured()
    {
        // 준비: 내 말 3번 칸, 상대 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));

        // 실행: 상대 말이 개로 내 말 칸(3번)에 도착
        bool moved = board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 내 말은 잡히지 않고 두 말이 같은 칸에 남는다
        Assert.IsTrue(moved);
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(myPiece.IsOnBoard);
        Assert.AreEqual(board.Node(3), myPiece.Node);
        Assert.AreEqual(board.Node(3), otherPiece.Node);
    }

    [Test]
    public void MyLandsOnOther_CannotCapture()
    {
        // 준비: 상대 말 3번 칸, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 3, board.Node(3), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));

        // 실행: 내 말이 개로 상대 말 칸(3번)에 도착
        bool moved = board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 상대 말을 잡지 못하고 두 말이 같은 칸에 남는다
        Assert.IsTrue(moved);
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(otherPiece.IsOnBoard);
        Assert.AreEqual(board.Node(3), otherPiece.Node);
        Assert.AreEqual(board.Node(3), myPiece.Node);
    }

    [Test]
    public void StackedMine_NoneCaptured()
    {
        // 준비: 내 말 두 개를 3번 칸에 업기, 상대 말 1번 칸
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.TryMove(myB, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 업힌 말도 모두 지켜진다
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(myA.IsOnBoard);
        Assert.IsTrue(myB.IsOnBoard);
    }

    [Test]
    public void FourPlayers_OnlyThirdTeamCaptured()
    {
        // 준비: 내 말 3번 칸(증강 보유), 제3팀 말이 3번 칸에 같이 있음, 상대 말 1번 칸
        //       제3팀 말이 내 말 칸에 와도 평화주의자라 안 잡히므로 같은 칸에 둘이 있게 된다
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        Piece thirdPiece = board.State.AddPiece(2, ThirdTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));
        board.State.TryMove(thirdPiece, 3, board.Node(3), out _);

        // 실행: 상대 말이 3번 칸에 도착
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 제3팀 말만 잡히고 내 말은 남는다
        CollectionAssert.AreEquivalent(new[] { thirdPiece }, result.CapturedPieces);
        Assert.IsTrue(thirdPiece.IsWaiting);
        Assert.IsTrue(myPiece.IsOnBoard);
    }

    [Test]
    public void TeamsWithoutAugment_CaptureNormally()
    {
        // 준비: 평화주의자는 내 팀만 가짐. 상대 말 3번 칸, 제3팀 말 1번 칸
        var board = new TestBoard();
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        Piece thirdPiece = board.State.AddPiece(2, ThirdTeam);
        board.State.TryMove(otherPiece, 3, board.Node(3), out _);
        board.State.TryMove(thirdPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));

        // 실행: 제3팀 말이 상대 말 칸에 도착
        board.State.TryMove(thirdPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 증강이 없는 팀끼리는 평소처럼 잡는다
        CollectionAssert.AreEquivalent(new[] { otherPiece }, result.CapturedPieces);
        Assert.IsTrue(otherPiece.IsWaiting);
    }

    [Test]
    public void SharedNode_MyPieceMovesAlone()
    {
        // 준비: 상대 말이 와도 안 잡혀서 내 말과 상대 말이 3번 칸에 같이 있음
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new PacifistAugment(MyTeam));
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 실행: 내 말이 도로 한 칸 이동
        board.State.TryMove(myPiece, YutResult.Do.Steps(), board.Node(4), out MoveResult result);

        // 검증: 상대 말은 업히지 않고 3번 칸에 남는다
        CollectionAssert.AreEquivalent(new[] { myPiece }, result.MovedPieces);
        Assert.AreEqual(board.Node(4), myPiece.Node);
        Assert.AreEqual(board.Node(3), otherPiece.Node);
    }
}