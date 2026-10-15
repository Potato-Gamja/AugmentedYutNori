using NUnit.Framework;

// 노장(Veteran): 내 말이 윷·모일 때만 1칸 덜 가고 나머지는 그대로인지, 노장 팀 말 수가 1개 적은지 검증하는 테스트
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

    [TestCase(YutResult.BackDo, -1)]
    [TestCase(YutResult.Do, 1)]
    [TestCase(YutResult.Gae, 2)]
    [TestCase(YutResult.Geol, 3)]
    [TestCase(YutResult.Yut, 3)]
    [TestCase(YutResult.Mo, 4)]
    public void MyPiece_MoveCount(YutResult rolled, int expected)
    {
        var (board, piece) = Setup(MyTeam);

        int count = board.State.GetModifiedMoveCount(piece, rolled.Steps());

        // 윷·모만 1칸 덜, 빽도·도·개·걸은 그대로
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
    public void MyPiece_BackDo_MovesBackOne()
    {
        // 준비: 빽도는 판 위 말만 쓸 수 있으므로 3번 칸에 먼저 놓고 증강을 붙인다
        var board = new TestBoard();
        Piece piece = board.State.AddPiece(0, MyTeam);
        board.State.TryMove(piece, 3, board.Node(3), out _);
        board.State.AddAugment(new VeteranAugment(MyTeam));

        bool moved = board.State.TryMove(piece, YutResult.BackDo.Steps(), board.Node(2), out _);

        // 빽도는 그대로 뒤로 1칸
        Assert.IsTrue(moved);
        Assert.AreEqual(board.Node(2), piece.Node);
    }

    [TestCase(YutResult.BackDo, -1)]
    [TestCase(YutResult.Do, 1)]
    [TestCase(YutResult.Gae, 2)]
    [TestCase(YutResult.Geol, 3)]
    [TestCase(YutResult.Yut, 4)]
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
    [TestCase(YutResult.Yut)]
    [TestCase(YutResult.Mo)]
    public void YutAndMo_KeepExtraThrow(YutResult result)
    {
        var (board, piece) = Setup(MyTeam);
        board.State.AddPiece(100, OtherTeam);
        var game = new GameState(board.State);
        var rules = new GameRules(game, new TurnManager(game, 2), new GameEvents(),
            new YutThrower(new SequenceRandomProvider(0.0)), new StandardGetResult(),
            new AugmentPool(new SequenceRandomProvider(0.0)));
        bool flat = result == YutResult.Yut;
        rules.ApplyThrow(new[] { flat, flat, flat, flat }, result);
        Assert.That(game.RemainingThrows, Is.EqualTo(1));
    }

    [Test]
    public void Setup_VeteranAndRemovedTeams_ExcludeLastSlotAndRemovedTeam()
    {
        var board = new TestBoard();
        board.State.AddAugment(new VeteranAugment(1));
        var setup = new GameSetup { teamCount = 3, piecesPerTeam = 4 };
        System.Func<int, int> count = team => team == 2 ? 0 :
            VeteranAugment.PieceCountFor(board.State, team, setup.piecesPerTeam);
        var pieces = new System.Collections.Generic.List<(int id, int team, int slot)>(setup.EnumeratePieces(count));
        Assert.That(pieces.Count, Is.EqualTo(7));
        Assert.That(pieces.Exists(piece => piece.id == 103 || piece.team == 2), Is.False);
        var scenePieces = pieces.ConvertAll(piece => (piece.team, piece.slot));
        Assert.That(setup.CheckScenePieces(scenePieces, count), Is.Empty);
        scenePieces.Add((1, 3));
        Assert.That(setup.CheckScenePieces(scenePieces, count), Has.Count.EqualTo(1));
    }
}