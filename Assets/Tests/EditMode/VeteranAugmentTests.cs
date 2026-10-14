using NUnit.Framework;

// 노장(Veteran): 내 말이 빽도면 0칸, 도는 그대로, 개~모는 1칸 덜 가는지 검증하는 테스트
public class VeteranAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    private const int BasePieces = 4;

    private static (TestBoard board, Piece piece) Setup(int team)
    {
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(team * GameSetup.TeamIdStride, team);
        board.State.AddAugment(new VeteranAugment(MyTeam));
        return (board, piece);
    }

    [TestCase(YutResult.Do, 1)]
    [TestCase(YutResult.Gae, 1)]
    [TestCase(YutResult.Geol, 2)]
    [TestCase(YutResult.Yut, 3)]
    [TestCase(YutResult.Mo, 4)]
    public void MyPiece_MoveCount(YutResult rolled, int expected)
    {
        var (board, piece) = Setup(MyTeam);

        int count = board.State.GetModifiedMoveCount(piece, rolled.Steps());

        // 도는 그대로, 개~모는 1칸 덜
        Assert.AreEqual(expected, count);
    }

    [Test]
    public void MyPiece_Mo_MovesFourOnBoard()
    {
        var (board, piece) = Setup(MyTeam);

        bool moved = board.State.TryMove(piece, YutResult.Mo.Steps(), board.Node(4), out _);

        // 대기 말 출발도 같은 보정: 모(5) - 1 = 4번 칸
        Assert.IsTrue(moved);
        Assert.AreEqual(board.Node(4), piece.Node);
    }

    [Test]
    public void MyPiece_BackDo_ZeroAndNoPath()
    {
        // 준비: 빽도는 판 위 말만 쓸 수 있으므로 3번 칸에 먼저 놓고 증강을 붙인다
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new VeteranAugment(MyTeam));

        int count = board.State.GetModifiedMoveCount(piece, YutResult.BackDo.Steps());

        // 빽도는 0칸 → 갈 수 있는 경로가 없어 쓸 수 없는 결과
        Assert.AreEqual(0, count);
        Assert.IsEmpty(board.State.GetMovePaths(piece, YutResult.BackDo.Steps()));
        Assert.AreEqual(board.Node(3), piece.Node);
    }

    [TestCase(YutResult.Do, 1)]
    [TestCase(YutResult.Mo, 5)]
    public void OtherPiece_Unchanged(YutResult rolled, int expected)
    {
        var (board, piece) = Setup(OtherTeam);

        int count = board.State.GetModifiedMoveCount(piece, rolled.Steps());

        // 내 증강은 상대 말에 적용되지 않는다
        Assert.AreEqual(expected, count);
    }

    [Test]
    public void PieceCount_VeteranTeam_OneLess()
    {
        var board = new TestBoard();
        board.State.AddAugment(new VeteranAugment(MyTeam));

        // 노장 팀은 말이 1개 적다
        Assert.AreEqual(BasePieces - 1, VeteranAugment.PieceCountFor(board.State, MyTeam, BasePieces));
    }

    [Test]
    public void PieceCount_OtherAugmentTeam_Unchanged()
    {
        var board = new TestBoard();
        board.State.AddAugment(new VeteranAugment(MyTeam));
        board.State.AddAugment(new MonkAugment(OtherTeam));

        // 다른 증강을 가진 팀은 그대로 (상대 팀 노장의 영향도 없음)
        Assert.AreEqual(BasePieces, VeteranAugment.PieceCountFor(board.State, OtherTeam, BasePieces));
    }

    [Test]
    public void PieceCount_NoAugment_Unchanged()
    {
        var board = new TestBoard();

        // 증강을 고르기 전(또는 증강 선택을 건너뛴 경우)에는 그대로
        Assert.AreEqual(BasePieces, VeteranAugment.PieceCountFor(board.State, MyTeam, BasePieces));
    }
}