// 프렌드쉴드(FriendShield): 업힌 내 말이 잡혀도 한 마리만 대기석으로 가는지 검증하는 테스트
using System.Linq;
using NUnit.Framework;

public class FriendShieldAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    // 기획: 살아남은 말은 잡으러 온 상대 말과 같은 칸에 남는다 (증강으로 살아남은 것)
    [Test]
    public void TwoStacked_OnlyOneCaptured()
    {
        // 준비: 내 말 두 개를 3번 칸에 업기, 상대 말 1번 칸
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.TryMove(myB, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new FriendShieldAugment(MyTeam));

        // 실행
        bool moved = board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 어느 말이 남는지는 내부 순서에 달려 있으므로 개수로 확인한다
        Piece[] myPieces = { myA, myB };
        Assert.IsTrue(moved);
        Assert.AreEqual(1, result.CapturedPieces.Count);
        Assert.AreEqual(1, myPieces.Count(p => p.IsOnBoard));

        Piece survivor = myPieces.First(p => p.IsOnBoard);
        Assert.AreEqual(otherPiece.Node, survivor.Node);
    }

    [Test]
    public void ThreeStacked_OnlyOneCaptured()
    {
        // 준비: 내 말 세 개를 3번 칸에 업기
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        Piece myC = board.State.AddPiece(3, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.TryMove(myB, 3, board.Node(3), out _);
        board.State.TryMove(myC, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new FriendShieldAugment(MyTeam));

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증
        Piece[] myPieces = { myA, myB, myC };
        Assert.AreEqual(1, result.CapturedPieces.Count);
        Assert.AreEqual(2, myPieces.Count(p => p.IsOnBoard));
    }

    [Test]
    public void SinglePiece_Captured()
    {
        // 준비: 업히지 않은 내 말 하나
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myPiece, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new FriendShieldAugment(MyTeam));

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 검증: 업힌 말이 아니면 평소처럼 잡힌다
        Assert.IsTrue(myPiece.IsWaiting);
    }

    [Test]
    public void OtherTeamStacked_AllCaptured()
    {
        // 준비: 상대 말 두 개를 3번 칸에 업기, 내 말 1번 칸
        var board = new TestBoard();
        Piece myPiece = board.State.AddPiece(0, MyTeam);
        Piece otherA = board.State.AddPiece(1, OtherTeam);
        Piece otherB = board.State.AddPiece(2, OtherTeam);
        board.State.TryMove(otherA, 3, board.Node(3), out _);
        board.State.TryMove(otherB, 3, board.Node(3), out _);
        board.State.TryMove(myPiece, 1, board.Node(1), out _);
        board.State.AddAugment(new FriendShieldAugment(MyTeam));

        // 실행: 내 말이 상대의 업힌 말을 잡는다
        board.State.TryMove(myPiece, YutResult.Gae.Steps(), board.Node(3), out MoveResult result);

        // 검증: 내 증강은 상대 말을 지키지 않는다
        Assert.AreEqual(2, result.CapturedPieces.Count);
        Assert.IsTrue(otherA.IsWaiting);
        Assert.IsTrue(otherB.IsWaiting);
    }

    [Test]
    public void AfterSaving_AugmentRemains()
    {
        // 준비
        var board = new TestBoard();
        Piece myA = board.State.AddPiece(0, MyTeam);
        Piece myB = board.State.AddPiece(2, MyTeam);
        Piece otherPiece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(myA, 3, board.Node(3), out _);
        board.State.TryMove(myB, 3, board.Node(3), out _);
        board.State.TryMove(otherPiece, 1, board.Node(1), out _);
        var sturdy = new FriendShieldAugment(MyTeam);
        board.State.AddAugment(sturdy);

        // 실행
        board.State.TryMove(otherPiece, YutResult.Gae.Steps(), board.Node(3), out _);

        // 검증: 여러 번 막아도 사라지지 않는다
        Assert.IsTrue(board.State.Augments.Contains(sturdy));
    }
}