// 청개구리(Contrarian): 내 말이 빽도를 내면 앞으로 1칸 경로도 고를 수 있는지 검증하는 테스트
using System.Collections.Generic;
using NUnit.Framework;

public class ContrarianAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    [Test]
    public void MyPiece_BackDo_CanGoBothWays()
    {
        // 준비: 3번 칸에 놓기
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new ContrarianAugment(MyTeam));

        // 실행
        List<BoardPath> paths = board.State.GetMovePaths(piece, YutResult.BackDo.Steps());

        // 검증: 뒤로 1칸(2번)과 앞으로 1칸(4번) 둘 다 후보
        Assert.AreEqual(2, paths.Count);
        Assert.IsTrue(paths.Exists(p => p.Destination == board.Node(2)));
        Assert.IsTrue(paths.Exists(p => p.Destination == board.Node(4)));
    }

    [Test]
    public void MyPiece_BackDo_ChooseForward_MovesToNext()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new ContrarianAugment(MyTeam));

        // 실행: 빽도를 앞으로 1칸으로 사용
        bool moved = board.State.TryMove(piece, YutResult.BackDo.Steps(), board.Node(4), out MoveResult result);

        // 검증
        Assert.IsTrue(moved);
        TestContext.WriteLine(result.Path);
        Assert.AreEqual(board.Node(4), piece.Node);
    }

    [Test]
    public void OtherPiece_BackDo_OnlyBackward()
    {
        // 준비: 증강은 내 팀, 말은 상대 팀
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(1, OtherTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new ContrarianAugment(MyTeam));

        // 실행
        List<BoardPath> paths = board.State.GetMovePaths(piece, YutResult.BackDo.Steps());

        // 검증: 평소처럼 뒤로 1칸만
        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual(board.Node(2), paths[0].Destination);
    }

    [Test]
    public void MyPiece_Do_NoExtraPath()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new ContrarianAugment(MyTeam));

        // 실행
        List<BoardPath> paths = board.State.GetMovePaths(piece, YutResult.Do.Steps());

        // 검증: 빽도가 아니면 경로가 추가되지 않는다
        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual(board.Node(4), paths[0].Destination);
    }

    // 기획: 청개구리 증강이 있으면 대기 말도 빽도로 앞 1칸 출발할 수 있다
    [Test]
    public void WaitingPiece_BackDo_CanStartForward()
    {
        // 준비
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.AddAugment(new ContrarianAugment(MyTeam));

        // 실행
        List<BoardPath> paths = board.State.GetMovePaths(piece, YutResult.BackDo.Steps());

        // 검증: 대기 말은 뒤로 못 가므로, 증강이 더한 앞 1칸 경로 하나만 있다
        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual(board.Node(1), paths[0].Destination);
    }
}