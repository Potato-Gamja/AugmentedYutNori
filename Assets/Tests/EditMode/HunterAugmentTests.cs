// 사냥꾼(Hunter): 잡으면 추가 던지기 2번, 대신 윷·모가 나와도 한 번 더 던지지 못하는지 검증하는 테스트
using NUnit.Framework;

public class HunterAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;
    private const int MyPieceId = 0;
    private const int OtherPieceId = 1;

    // StandardGetResult 기준 윷가락 (true = 평평한 면, 0번이 표시된 막대)
    private static readonly bool[] GaeSticks = { true, true, false, false };
    private static readonly bool[] YutSticks = { true, true, true, true };
    private static readonly bool[] MoSticks = { false, false, false, false };

    // 팀마다 대기 말 1개. hunterTeam이 사냥꾼을 가진다 (null이면 아무도 없음)
    // otherPieceNode: 상대 말을 미리 놓을 칸 (0이면 대기 중)
    private static GameRules MakeRules(int? hunterTeam, out GameState game, int otherPieceNode = 0)
    {
        var board = new TestBoard();
        board.State.AddPiece(MyPieceId, MyTeam);
        Piece otherPiece = board.State.AddPiece(OtherPieceId, OtherTeam);
        if (otherPieceNode > 0) board.State.TryMove(otherPiece, otherPieceNode, board.Node(otherPieceNode), out _);
        if (hunterTeam.HasValue) board.State.AddAugment(new HunterAugment(hunterTeam.Value));

        game = new GameState(board.State);
        var turn = new TurnManager(game, 2);
        return new GameRules(
            game,
            turn,
            new GameEvents(),
            new YutThrower(new SequenceRandomProvider(0.0)),
            new StandardGetResult(),
            new AugmentPool(new SequenceRandomProvider(0.0)));
    }

    // ───── 윷·모 ─────

    [Test]
    public void Yut_NoExtraThrow()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행
        rules.ApplyThrow(YutSticks, YutResult.Yut);

        // 검증: 사냥꾼은 윷이 나와도 한 번 더 던지지 못한다
        Assert.AreEqual(0, game.RemainingThrows);
    }

    [Test]
    public void Mo_NoExtraThrow()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행
        rules.ApplyThrow(MoSticks, YutResult.Mo);

        // 검증
        Assert.AreEqual(0, game.RemainingThrows);
    }

    [Test]
    public void TeamWithoutHunter_YutGrantsExtraThrow()
    {
        // 준비: 사냥꾼은 상대 팀만 가짐, 지금은 내 차례
        GameRules rules = MakeRules(OtherTeam, out GameState game);

        // 실행
        rules.ApplyThrow(YutSticks, YutResult.Yut);

        // 검증: 사냥꾼이 아닌 팀은 평소처럼 한 번 더
        Assert.AreEqual(1, game.RemainingThrows);
    }

    // ───── 잡기 ─────

    [Test]
    public void Capture_TwoExtraThrows()
    {
        // 준비: 상대 말 2번 칸, 내가 개를 던짐
        GameRules rules = MakeRules(MyTeam, out GameState game, otherPieceNode: 2);
        rules.ApplyThrow(GaeSticks, YutResult.Gae);

        // 실행: 내 말이 2번 칸으로 가서 상대 말을 잡음
        bool moved = rules.MovePiece(MyPieceId, 0, 2);

        // 검증: 사냥꾼은 잡으면 두 번 더 던진다
        Assert.IsTrue(moved);
        Assert.IsTrue(game.Board.GetPiece(OtherPieceId).IsWaiting);
        Assert.AreEqual(2, game.RemainingThrows);
    }

    [Test]
    public void TeamWithoutHunter_CaptureOneExtraThrow()
    {
        // 준비: 아무도 사냥꾼이 없음
        GameRules rules = MakeRules(null, out GameState game, otherPieceNode: 2);
        rules.ApplyThrow(GaeSticks, YutResult.Gae);

        // 실행
        rules.MovePiece(MyPieceId, 0, 2);

        // 검증: 평소처럼 한 번 더
        Assert.AreEqual(1, game.RemainingThrows);
    }

    [Test]
    public void BonusThrow_Yut_NoMoreExtra()
    {
        // 준비: 잡아서 추가 던지기 2번을 받음
        GameRules rules = MakeRules(MyTeam, out GameState game, otherPieceNode: 2);
        rules.ApplyThrow(GaeSticks, YutResult.Gae);
        rules.MovePiece(MyPieceId, 0, 2);

        // 실행: 추가 던지기 첫 번째에서 윷
        rules.ApplyThrow(YutSticks, YutResult.Yut);

        // 검증: 윷이어도 늘지 않고 1번만 남는다
        Assert.AreEqual(1, game.RemainingThrows);
    }
}