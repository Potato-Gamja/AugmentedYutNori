// 빠른 출발(QuickStart): 대기 중인 내 말이 판에 나올 때 1칸 더 가는지 검증하는 테스트
using NUnit.Framework;

public class QuickStartAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    [Test]
    public void WaitingPiece_Do_MovesTwo()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new QuickStartAugment(MyTeam));

        // 실행
        bool moved = board.State.TryMove(piece, YutResult.Do.Steps(), board.Node(2), out MoveResult result);

        // 검증: 도(1) + 1 = 2번 칸
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(2), piece.Node);
    }

    [Test]
    public void WaitingPiece_Mo_MovesSix()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new QuickStartAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Mo.Steps());

        // 검증: 모(5) + 1 = 6
        Assert.AreEqual(6, count);
    }

    [Test]
    public void OnBoardPiece_Do_Unchanged()
    {
        // 준비: 이미 판 위에 있는 말
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new QuickStartAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Do.Steps());

        // 검증: 출발하는 말이 아니면 보너스 없음
        Assert.AreEqual(1, count);
    }

    [Test]
    public void OtherWaitingPiece_Do_Unchanged()
    {
        // 준비: 증강은 내 팀, 대기 중인 말은 상대 팀
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(1, OtherTeam);
        board.State.AddAugment(new QuickStartAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Do.Steps());

        // 검증
        Assert.AreEqual(1, count);
    }

    [Test]
    public void WaitingPiece_BackDo_Unchanged()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new QuickStartAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.BackDo.Steps());

        // 검증: 빽도에 +1이 붙으면 0칸이 되므로 그대로 -1이어야 한다
        Assert.AreEqual(-1, count);
    }
}