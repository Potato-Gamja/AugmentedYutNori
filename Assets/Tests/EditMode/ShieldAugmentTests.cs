// 방패(Shield): 내 말이 처음 잡힐 때 한 번 무효화되고 증강이 사라지는지 검증하는 테스트
using NUnit.Framework;
using System.Linq;

public class ShieldAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    [Test]
    public void OtherPieceLandsOnMine_MyPieceNotCaptured()
    {
        // 준비: 내 말 3번 칸, 상대 말 1번 칸, 방패는 배치 후에
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new ShieldAugment(MyTeam));

        // 실행: 상대 말이 개로 내 말 칸(3번)에 도착
        bool moved = board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증
        Assert.IsTrue(moved);
        Assert.IsEmpty(result.CapturedPieces);
        Assert.IsTrue(myPiece.IsOnBoard);
        Assert.AreEqual(board.Node(3), myPiece.Node);
    }

    [Test]
    public void AfterBlocking_ShieldIsRemoved()
    {
        // 준비
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        var shield = new ShieldAugment(MyTeam);
        board.State.AddAugment(shield);

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 검증: 1회용이므로 막고 나면 사라진다
        Assert.IsFalse(board.State.Augments.Contains(shield));
    }

    [Test]
    public void SecondCapture_MyPieceCaptured()
    {
        // 준비: 상대 말 두 개 (1번 칸, 2번 칸)
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherA = board.State.AddPiece(1, OtherTeam);
        Piece otherB = board.State.AddPiece(2, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherA, 1, board.Node(1), out _);
        board.State.TryMove(otherB, 2, board.Node(2), out _);
        board.State.AddAugment(new ShieldAugment(MyTeam));

        // 실행: 첫 번째는 방패가 막고, 두 번째 상대 말이 다시 내 말 칸에 도착
        board.State.TryMove(otherA, YutResult.Gae.Steps(), board.Node(3), out _);
        bool moved = board.State.TryMove(otherB, YutResult.Do.Steps(), board.Node(3), out MoveResult result);

        // 검증: 방패가 없으니 이번엔 잡힌다
        Assert.IsTrue(moved);
        CollectionAssert.Contains(result.CapturedPieces, myPiece);
        Assert.IsTrue(myPiece.IsWaiting);
    }

    [Test]
    public void StackedPieces_BothSaved_ShieldUsedOnce()
    {
        // 준비: 내 말 두 개를 3번 칸에 업기
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.TryMove(myB, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        var shield = new ShieldAugment(MyTeam);
        board.State.AddAugment(shield);

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 검증: 업힌 말은 한 번에 같이 지켜지고 방패는 한 번만 소모된다
        Assert.IsTrue(myA.IsOnBoard);
        Assert.IsTrue(myB.IsOnBoard);
        Assert.IsFalse(board.State.Augments.Contains(shield));
    }

    [Test]
    public void MyPieceCaptures_ShieldNotUsed()
    {
        // 준비: 반대로 상대 말 3번 칸, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(otherPiece, 3, board.Node(3), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        var shield = new ShieldAugment(MyTeam);
        board.State.AddAugment(shield);

        // 실행: 내 말이 상대 말을 잡는다
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 검증: 상대 말은 잡히고, 공격할 때는 방패가 소모되지 않는다
        Assert.IsTrue(otherPiece.IsWaiting);
        Assert.IsTrue(board.State.Augments.Contains(shield));
    }
}