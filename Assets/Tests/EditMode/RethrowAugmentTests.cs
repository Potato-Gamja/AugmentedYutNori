// 야바위꾼(Rethrow): 사용 조건 5개(내 차례·보유 / 윷·모 아님 / 이동 전 / 턴의 첫 굴리기 / 턴당 1번)와 소모되지 않는지 검증하는 테스트
using NUnit.Framework;

public class RethrowAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    // StandardGetResult 기준 윷가락 (true = 평평한 면, 0번이 표시된 막대)
    private static readonly bool[] BackDoSticks = { true, false, false, false };
    private static readonly bool[] DoSticks = { false, true, false, false };
    private static readonly bool[] GaeSticks = { true, true, false, false };
    private static readonly bool[] GeolSticks = { true, true, true, false };

    // 야바위꾼을 가진 게임 상태를 만든다. 결과는 각 테스트에서 StoredResults에 직접 넣는다
    private static GameState MakeGame(out RethrowAugment rethrow)
    {
        var board = new TestBoard();
        var game = new GameState(board.State);
        rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);
        return game;
    }

    // 실제 던지기 흐름을 확인할 GameRules를 만든다 (각 팀 대기 말 1개, 팀 0이 야바위꾼 보유)
    private static GameRules MakeRules(out GameState game, out TurnManager turn, out RethrowAugment rethrow, bool ownsRethrow = true)
    {
        var board = new TestBoard();
        board.State.AddPiece(0, MyTeam);
        board.State.AddPiece(1, OtherTeam);
        game = new GameState(board.State);
        turn = new TurnManager(game, 2);
        rethrow = new RethrowAugment(MyTeam);
        if (ownsRethrow) board.State.AddAugment(rethrow);

        return new GameRules(
            game,
            turn,
            new GameEvents(),
            new YutThrower(new SequenceRandomProvider(0.0)),
            new StandardGetResult(),
            new AugmentPool(new SequenceRandomProvider(0.0)));
    }

    // ───── CanUse 조건 ─────

    [Test]
    public void FirstThrow_Do_CanUse()
    {
        // 준비: 턴의 첫 결과가 도
        GameState game = MakeGame(out RethrowAugment rethrow);
        game.StoredResults.Add(YutResult.Do);

        // 실행 + 검증
        Assert.IsTrue(rethrow.CanUse(game, MyTeam));
    }

    [Test]
    public void OtherTurn_CannotUse()
    {
        // 준비
        GameState game = MakeGame(out RethrowAugment rethrow);
        game.StoredResults.Add(YutResult.Do);

        // 실행 + 검증: 상대 턴에는 쓸 수 없다
        Assert.IsFalse(rethrow.CanUse(game, OtherTeam));
    }

    [Test]
    public void NotOwned_CannotUse()
    {
        // 준비: 판에 등록하지 않은 증강
        var board = new TestBoard();
        var game = new GameState(board.State);
        var rethrow = new RethrowAugment(MyTeam);
        game.StoredResults.Add(YutResult.Do);

        // 실행 + 검증
        Assert.IsFalse(rethrow.CanUse(game, MyTeam));
    }

    [Test]
    public void NoResult_CannotUse()
    {
        // 준비: 아직 던지지 않음
        GameState game = MakeGame(out RethrowAugment rethrow);

        // 실행 + 검증
        Assert.IsFalse(rethrow.CanUse(game, MyTeam));
    }

    [Test]
    public void FirstThrow_YutOrMo_CannotUse()
    {
        // 준비
        GameState yutGame = MakeGame(out RethrowAugment yutRethrow);
        yutGame.StoredResults.Add(YutResult.Yut);
        GameState moGame = MakeGame(out RethrowAugment moRethrow);
        moGame.StoredResults.Add(YutResult.Mo);

        // 실행 + 검증: 윷·모는 이미 한 번 더 던지므로 쓸 수 없다
        Assert.IsFalse(yutRethrow.CanUse(yutGame, MyTeam));
        Assert.IsFalse(moRethrow.CanUse(moGame, MyTeam));
    }

    [Test]
    public void SecondThrow_CannotUse()
    {
        // 준비: 윷이 나와 한 번 더 던졌더니 개
        GameState game = MakeGame(out RethrowAugment rethrow);
        game.StoredResults.Add(YutResult.Yut);
        game.StoredResults.Add(YutResult.Gae);

        // 실행 + 검증: 턴의 첫 굴리기가 아니다
        Assert.IsFalse(rethrow.CanUse(game, MyTeam));
    }

    [Test]
    public void AfterMoved_CannotUse()
    {
        // 준비: 잡아서 추가로 던진 상황 (이미 말을 움직였고 결과는 1개)
        GameState game = MakeGame(out RethrowAugment rethrow);
        game.StoredResults.Add(YutResult.Do);
        game.HasMovedThisTurn = true;

        // 실행 + 검증
        Assert.IsFalse(rethrow.CanUse(game, MyTeam));
    }

    [Test]
    public void AlreadyRethrown_CannotUse()
    {
        // 준비: 이번 턴에 이미 다시 던짐
        GameState game = MakeGame(out RethrowAugment rethrow);
        game.StoredResults.Add(YutResult.Do);
        game.HasRethrownThisTurn = true;

        // 실행 + 검증: 턴당 1번
        Assert.IsFalse(rethrow.CanUse(game, MyTeam));
    }

    // ───── GameRules 흐름 ─────

    [Test]
    public void ApplyRethrow_ReplacesResult_AugmentRemains()
    {
        // 준비: 첫 굴리기 도
        GameRules rules = MakeRules(out GameState game, out _, out RethrowAugment rethrow);
        rules.ApplyThrow(DoSticks, YutResult.Do);

        // 실행: 개로 다시 던짐
        bool rethrown = rules.ApplyRethrow(GaeSticks, YutResult.Gae);

        // 검증: 결과만 바뀌고 증강은 남는다 (1회용 아님)
        Assert.IsTrue(rethrown);
        CollectionAssert.AreEqual(new[] { YutResult.Gae }, game.StoredResults);
        Assert.IsTrue(game.HasRethrownThisTurn);
        Assert.IsTrue(game.Board.GetAugments(MyTeam).Contains(rethrow));
    }

    [Test]
    public void ApplyRethrow_TwiceInSameTurn_SecondFails()
    {
        // 준비: 첫 굴리기 도 → 개로 다시 던짐
        GameRules rules = MakeRules(out GameState game, out _, out _);
        rules.ApplyThrow(DoSticks, YutResult.Do);
        rules.ApplyRethrow(GaeSticks, YutResult.Gae);

        // 실행: 같은 턴에 한 번 더
        bool second = rules.ApplyRethrow(GeolSticks, YutResult.Geol);

        // 검증: 실패하고 결과도 그대로
        Assert.IsFalse(second);
        CollectionAssert.AreEqual(new[] { YutResult.Gae }, game.StoredResults);
    }

    [Test]
    public void NextTurn_CanUseAgain()
    {
        // 준비: 팀 0이 이번 턴에 다시 던짐
        GameRules rules = MakeRules(out GameState game, out TurnManager turn, out RethrowAugment rethrow);
        rules.ApplyThrow(DoSticks, YutResult.Do);
        rules.ApplyRethrow(GaeSticks, YutResult.Gae);

        // 실행: 팀 1 → 다시 팀 0 차례가 되어 첫 굴리기 도
        turn.NextTurn();
        turn.NextTurn();
        rules.ApplyThrow(DoSticks, YutResult.Do);

        // 검증: 턴이 바뀌면 다시 쓸 수 있다
        Assert.AreEqual(MyTeam, turn.CurrentTeam);
        Assert.IsFalse(game.HasRethrownThisTurn);
        Assert.IsTrue(rethrow.CanUse(game, MyTeam));
    }
    [Test]
    public void FirstBackDoWithOnlyWaitingPieces_PreservesTurnAndAllowsRethrow()
    {
        GameRules rules = MakeRules(out GameState game, out TurnManager turn, out RethrowAugment rethrow);
        rules.ApplyThrow(BackDoSticks, YutResult.BackDo);
        Assert.AreEqual(MyTeam, turn.CurrentTeam);
        Assert.IsFalse(rules.HasAnyStoredMove());
        Assert.IsTrue(rethrow.CanUse(game, MyTeam));
        CollectionAssert.AreEqual(new[] { YutResult.BackDo }, game.StoredResults);
        Assert.IsTrue(rules.ApplyRethrow(DoSticks, YutResult.Do));
        Assert.AreEqual(MyTeam, turn.CurrentTeam);
        CollectionAssert.AreEqual(new[] { YutResult.Do }, game.StoredResults);
    }

    [Test]
    public void BackDoWithoutRethrowAugment_EndsTurn()
    {
        GameRules rules = MakeRules(out GameState game, out TurnManager turn, out _, ownsRethrow: false);
        rules.ApplyThrow(BackDoSticks, YutResult.BackDo);
        Assert.AreEqual(OtherTeam, turn.CurrentTeam);
        Assert.IsEmpty(game.StoredResults);
    }

    [Test]
    public void RethrownBackDoWithNoMove_EndsTurnInsteadOfWaitingForAnotherRethrow()
    {
        GameRules rules = MakeRules(out GameState game, out TurnManager turn, out RethrowAugment rethrow);
        rules.ApplyThrow(BackDoSticks, YutResult.BackDo);
        Assert.IsTrue(rules.ApplyRethrow(BackDoSticks, YutResult.BackDo));
        Assert.AreEqual(OtherTeam, turn.CurrentTeam);
        Assert.IsEmpty(game.StoredResults);
        Assert.IsTrue(game.Board.GetAugments(MyTeam).Contains(rethrow));
    }

    [Test]
    public void BackDoAfterMoving_CannotHoldTurnForRethrow()
    {
        GameRules rules = MakeRules(out GameState game, out TurnManager turn, out _);
        game.HasMovedThisTurn = true;
        rules.ApplyThrow(BackDoSticks, YutResult.BackDo);
        Assert.AreEqual(OtherTeam, turn.CurrentTeam);
        Assert.IsEmpty(game.StoredResults);
    }

}