// 도박사(Gambler): 던진 결과가 도↔모로 바뀌고, 바뀐 결과는 추가 던지기가 없는지 검증하는 테스트
using NUnit.Framework;

public class GamblerAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;
    private const int MyPieceId = 0;
    private const int OtherPieceId = 1;

    // StandardGetResult 기준 윷가락 (true = 평평한 면, 0번이 표시된 막대)
    private static readonly bool[] DoSticks = { false, true, false, false };
    private static readonly bool[] BackDoSticks = { true, false, false, false };
    private static readonly bool[] GaeSticks = { true, true, false, false };
    private static readonly bool[] YutSticks = { true, true, true, true };
    private static readonly bool[] MoSticks = { false, false, false, false };

    // 팀마다 대기 말 1개. gamblerTeam이 도박사를 가진다
    private static GameRules MakeRules(int gamblerTeam, out GameState game)
    {
        var board = new TestBoard();
        board.State.AddPiece(MyPieceId, MyTeam);
        board.State.AddPiece(OtherPieceId, OtherTeam);
        board.State.AddAugment(new GamblerAugment(gamblerTeam));

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

    // ───── 도↔모 ─────

    [Test]
    public void Do_BecomesMo_NoExtraThrow()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행: 도를 던짐
        YutThrowOutcome outcome = rules.ApplyThrow(DoSticks, YutResult.Do);

        // 검증: 모로 바뀌어 저장되고, 바뀐 모는 한 번 더 던지지 않는다
        Assert.AreEqual(YutResult.Mo, outcome.Result);
        CollectionAssert.AreEqual(new[] { YutResult.Mo }, game.StoredResults);
        Assert.AreEqual(0, game.RemainingThrows);

        // 검증: 화면 안내용으로 원래 결과(도)와 바뀌었다는 사실이 남아 있다
        Assert.AreEqual(YutResult.Do, outcome.OriginalResult);
        Assert.IsTrue(outcome.IsResultChanged);
    }

    [Test]
    public void Mo_BecomesDo_NoExtraThrow()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행: 모를 던짐
        YutThrowOutcome outcome = rules.ApplyThrow(MoSticks, YutResult.Mo);

        // 검증: 도로 바뀌고, 원래 모의 추가 던지기도 없어진다
        Assert.AreEqual(YutResult.Do, outcome.Result);
        CollectionAssert.AreEqual(new[] { YutResult.Do }, game.StoredResults);
        Assert.AreEqual(0, game.RemainingThrows);

        // 검증: 원래 결과(모)가 남아 있다
        Assert.AreEqual(YutResult.Mo, outcome.OriginalResult);
        Assert.IsTrue(outcome.IsResultChanged);
    }

    // ───── 바뀌지 않는 결과 ─────

    [Test]
    public void Yut_Unchanged_ExtraThrow()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행
        YutThrowOutcome outcome = rules.ApplyThrow(YutSticks, YutResult.Yut);

        // 검증: 윷은 바뀌지 않으므로 평소처럼 한 번 더
        CollectionAssert.AreEqual(new[] { YutResult.Yut }, game.StoredResults);
        Assert.AreEqual(1, game.RemainingThrows);
        Assert.IsFalse(outcome.IsResultChanged);
    }

    [Test]
    public void Gae_Unchanged()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행
        YutThrowOutcome outcome = rules.ApplyThrow(GaeSticks, YutResult.Gae);

        // 검증: 결과도 원래 결과도 개, 바뀌지 않음
        Assert.AreEqual(YutResult.Gae, outcome.Result);
        Assert.AreEqual(YutResult.Gae, outcome.OriginalResult);
        Assert.IsFalse(outcome.IsResultChanged);
    }

    [Test]
    public void BackDo_Unchanged()
    {
        // 준비
        GameRules rules = MakeRules(MyTeam, out GameState game);

        // 실행: 빽도 (표시된 막대 하나만 평평 → 도와 다른 결과)
        YutThrowOutcome outcome = rules.ApplyThrow(BackDoSticks, YutResult.BackDo);

        // 검증: 빽도는 바뀌지 않는다 (판에 말이 없어 바로 턴이 넘어가므로 반환값으로 확인)
        Assert.AreEqual(YutResult.BackDo, outcome.Result);
        Assert.IsFalse(outcome.IsResultChanged);
    }

    // ───── 다른 팀 / 실제 이동 ─────

    [Test]
    public void TeamWithoutGambler_Unaffected()
    {
        // 준비: 도박사는 상대 팀만 가짐, 지금은 내 차례
        GameRules rules = MakeRules(OtherTeam, out GameState game);

        // 실행: 모를 던짐
        YutThrowOutcome outcome = rules.ApplyThrow(MoSticks, YutResult.Mo);

        // 검증: 그대로 모이고 한 번 더 던진다
        CollectionAssert.AreEqual(new[] { YutResult.Mo }, game.StoredResults);
        Assert.AreEqual(1, game.RemainingThrows);
        Assert.IsFalse(outcome.IsResultChanged);
    }

    [Test]
    public void SwappedMo_MovesFiveSteps()
    {
        // 준비: 도를 던져 모로 바뀜
        GameRules rules = MakeRules(MyTeam, out GameState game);
        rules.ApplyThrow(DoSticks, YutResult.Do);

        // 실행: 바뀐 모(5칸)로 대기 말 이동
        bool moved = rules.MovePiece(MyPieceId, 0, 5);

        // 검증: 실제로 5번 칸까지 간다
        Assert.IsTrue(moved);
        Assert.AreEqual(5, game.Board.GetPiece(MyPieceId).Node.Id);
    }
}