using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class GameEffectFeedbackTests
{
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Presenter(TestBoard board, GameState state) => Activator.CreateInstance(
        Runtime("GameFlowHudPresenter"), new object[] { null, board.State, state, null, null, new Func<int>(() => 0) });
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method).Invoke(owner, args);

    private static string Name(string id) => new AugmentCatalog(
        UnityEngine.JsonUtility.FromJson<AugmentDefinitionFile>(System.IO.File.ReadAllText("Assets/Data/augments.json")),
        AugmentFactories.Create()).Find(id).name;

    [TestCase("Contrarian", YutResult.BackDo, true)]
    [TestCase("Contrarian", YutResult.Do, false)]
    [TestCase("Monk", YutResult.Do, true)]
    [TestCase("Monk", YutResult.Gae, true)]
    [TestCase("Monk", YutResult.Geol, false)]
    [TestCase("Veteran", YutResult.Yut, true)]
    [TestCase("Veteran", YutResult.Mo, true)]
    [TestCase("Veteran", YutResult.Gae, false)]
    [TestCase("Marathoner", YutResult.Do, true)]
    [TestCase("Marathoner", YutResult.BackDo, false)]
    [TestCase("Hunter", YutResult.Yut, true)]
    [TestCase("Hunter", YutResult.Do, false)]
    [TestCase("Gambler", YutResult.Gae, false)]
    public void ThrowEffectsOnlyShowWhenTriggered(string id, YutResult result, bool expected)
    {
        var board = new TestBoard(); board.State.AddPiece(0, 0);
        board.State.AddAugment(AugmentFactories.Create()[id](0));
        var state = new GameState(board.State);
        string text = (string)Call(Presenter(board, state), "DescribeThrowEffects",
            new YutThrowOutcome(new bool[4], result), 0, result.GrantsExtraThrow() && id != "Hunter");
        Assert.That(text.Split('\n').Contains(Name(id)), Is.EqualTo(expected));
        Assert.That(text, Does.Not.Contain("효과"));
        if (id == "Hunter" && expected) Assert.That(text, Does.Contain("추가 던지기 없음"));
    }

    [TestCase("Sprinter", 1, 2)]
    [TestCase("Monk", 2, 3)]
    [TestCase("Marathoner", 1, 2)]
    [TestCase("Veteran", 4, 3)]
    [TestCase("Contrarian", -1, 1)]
    public void MovementAugmentsShowOnAppliedMove(string id, int roll, int destination)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0);
        board.State.AddAugment(AugmentFactories.Create()[id](0));
        Assert.That(board.State.TryMove(piece, roll, board.Node(destination), out var result), Is.True);
        string text = (string)Call(Presenter(board, new GameState(board.State)), "DescribeMoveEffects", result);
        Assert.That(text, Does.Contain(Name(id)));
    }

    [TestCase("Assassin", 2)]
    [TestCase("Hunter", 1)]
    [TestCase("Pacifist", 1)]
    public void CaptureAugmentFeedbackUsesActualMove(string id, int enemyNode)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0); var enemy = board.State.AddPiece(100, 1);
        board.State.TryMove(enemy, enemyNode, board.Node(enemyNode), out _);
        board.State.AddAugment(AugmentFactories.Create()[id](0));
        Assert.That(board.State.TryMove(piece, 1, board.Node(1), out var result), Is.True);
        string text = (string)Call(Presenter(board, new GameState(board.State)), "DescribeMoveEffects", result);
        Assert.That(text, Does.Contain(Name(id)));
        if (id == "Hunter") Assert.That(text, Does.Contain("추가 던지기 2회"));
    }

    [TestCase("Pacifist")]
    [TestCase("Loner")]
    [TestCase("FriendShield")]
    public void DefensiveAugmentFeedbackAppearsWhenPieceIsProtected(string id)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0);
        var enemy = board.State.AddPiece(100, 1); board.State.TryMove(enemy, 1, board.Node(1), out _);
        if (id == "FriendShield")
        {
            var stacked = board.State.AddPiece(101, 1); board.State.TryMove(stacked, 1, board.Node(1), out _);
        }
        board.State.AddAugment(AugmentFactories.Create()[id](1));
        Assert.That(board.State.TryMove(piece, 1, board.Node(1), out var result), Is.True);
        Assert.That((string)Call(Presenter(board, new GameState(board.State)), "DescribeMoveEffects", result), Does.Contain(Name(id)));
    }

    [Test]
    public void RethrowEffectAppearsOnceAndGamblerShowsItsTransformation()
    {
        var board = new TestBoard(); board.State.AddPiece(0, 0); board.State.AddAugment(new RethrowAugment(0));
        var state = new GameState(board.State) { HasRethrownThisTurn = true }; object presenter = Presenter(board, state);
        var outcome = new YutThrowOutcome(new bool[4], YutResult.Do);
        Assert.That((string)Call(presenter, "DescribeThrowEffects", outcome, 0, false), Does.Contain("야바위꾼"));
        Assert.That((string)Call(presenter, "DescribeThrowEffects", outcome, 0, false), Does.Not.Contain("야바위꾼"));
        board.State.AddAugment(new GamblerAugment(0));
        outcome = new YutThrowOutcome(new bool[4], YutResult.Mo, YutResult.Do);
        Assert.That((string)Call(presenter, "DescribeThrowEffects", outcome, 0, false), Does.Contain("도박사"));
    }

    [TestCase(false, false, 0, true)]
    [TestCase(true, false, 0, false)]
    [TestCase(false, true, 0, false)]
    [TestCase(false, false, 1, false)]
    public void BackDoNoticeDoesNotShowWhenAnotherActionRemains(bool rethrow, bool hasMove, int throws, bool expected)
    {
        var board = new TestBoard(); board.State.AddPiece(0, 0);
        if (rethrow) board.State.AddAugment(new RethrowAugment(0));
        var state = new GameState(board.State) { RemainingThrows = throws }; state.StoredResults.Add(YutResult.BackDo);
        Assert.That(Call(Presenter(board, state), "ShouldShowSkippedBackDo",
            new YutThrowOutcome(new bool[4], YutResult.BackDo), 0, hasMove), Is.EqualTo(expected));
    }

    [Test]
    public void MultipleResultsDoNotCreateSelectionBadge()
    {
        var controller = Activator.CreateInstance(Runtime("GameFlowSelectionController"), new object[]
        { null, new TestBoard().State, null, null, null, null, new Func<int>(() => 0), new Action<MoveOption>(_ => {}), null });
        var options = new System.Collections.Generic.List<MoveOption>
        { new MoveOption(0,YutResult.Do,1,1,false), new MoveOption(1,YutResult.Do,1,1,false) };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        controller.GetType().GetMethod("CreateLabel", flags).Invoke(controller, new object[] { null, options });
        Assert.That(((IList)controller.GetType().GetField("labels", flags).GetValue(controller)).Count, Is.Zero);
    }
}
