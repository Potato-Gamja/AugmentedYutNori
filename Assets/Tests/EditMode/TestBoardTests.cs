using NUnit.Framework;

// TestBoard가 실제 윷놀이 규칙대로 판을 연결했는지 검증하는 테스트
public class TestBoardTests
{
    [Test]
    public void WaitingPiece_Do_MovesToFirstNode()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, 0);

        // 실행
        bool moved = board.State.TryMove(piece, YutResult.Do.Steps(), board.Node(1), out MoveResult result);

        // 검증
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(1), piece.Node);
        Assert.IsTrue(piece.IsOnBoard);    
    }

    [Test]
    public void FirstNodePiece_BackDo_MovesToEnd()
    {
        // 준비: 1번 칸에 놓기
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, 0);
        board.State.TryMove(piece, 1, board.Node(1), out _);

        // 실행
        bool moved = board.State.TryMove(piece, YutResult.BackDo.Steps(), board.Node(10), out MoveResult result);

        // 검증: 첫 칸에서 빽도하면 대기석이 아니라 참먹이(End)로 간다
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(10), piece.Node);
        Assert.IsTrue(piece.IsOnBoard);
    }

    [Test]
    public void Piece_Gae_StopsOnEnd_NotFinished()
    {
        // 준비: 8번 칸에 놓기
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, 0);
        board.State.TryMove(piece, 8, board.Node(8), out _);

        // 실행
        bool moved = board.State.TryMove(piece, YutResult.Gae.Steps(), board.Node(10), out MoveResult result);

        // 검증: 참먹이에 멈춘 것만으로는 골인이 아니다
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(10), piece.Node);
        Assert.IsFalse(piece.IsFinished);
    }

    [Test]
    public void Piece_Geol_PassesEnd_Finishes()
    {
        // 준비: 8번 칸에 놓기
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, 0);
        board.State.TryMove(piece, 8, board.Node(8), out _);

        // 실행: 9 → 10(End) → 0(Start)으로 들어가며 골인. 골인 경로의 도착 칸은 Start
        bool moved = board.State.TryMove(piece, YutResult.Geol.Steps(), board.Node(0), out MoveResult result);

        // 검증
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.IsTrue(result.Path.IsFinished);
        Assert.IsTrue(piece.IsFinished);
    }
}
