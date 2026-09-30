using NUnit.Framework;

// 잔걸음 강화(WeakBoost): 내 말이 도·개를 내면 1칸 더 가는지 검증하는 테스트
public class WeakBoostAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    [Test]
    public void MyPiece_Do_MovesTwo()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new WeakBoostAugment(MyTeam));

        // 실행
        bool moved = board.State.TryMove(piece, YutResult.Do.Steps(), board.Node(2), out MoveResult result);

        // 검증: 도(1) + 1 = 2번 칸
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(2), piece.Node);
    }

    [Test]
    public void MyPiece_Gae_MovesThree()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new WeakBoostAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Gae.Steps());

        // 검증: 개(2) + 1 = 3
        Assert.AreEqual(3, count);
    }

    [Test]
    public void MyPiece_Geol_Unchanged()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new WeakBoostAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Geol.Steps());

        // 검증: 도·개가 아니면 보너스 없음
        Assert.AreEqual(3, count);
    }

    [Test]
    public void MyPiece_BackDo_Unchanged()
    {
        // 준비: 빽도는 판 위 말만 쓸 수 있으므로 3번 칸에 먼저 놓고 증강을 붙인다
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new WeakBoostAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.BackDo.Steps());

        // 검증: 빽도에 +1이 붙으면 0칸(제자리)이 되므로 그대로 -1이어야 한다
        Assert.AreEqual(-1, count);
    }

    [Test]
    public void OtherPiece_Do_Unchanged()
    {
        // 준비: 증강은 내 팀, 움직이는 말은 상대 팀
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(1, OtherTeam);
        board.State.AddAugment(new WeakBoostAugment(MyTeam));

        // 실행
        int count = board.State.GetModifiedMoveCount(piece, YutResult.Do.Steps());

        // 검증: 내 증강은 상대 말에 적용되지 않는다
        Assert.AreEqual(1, count);
    }
}