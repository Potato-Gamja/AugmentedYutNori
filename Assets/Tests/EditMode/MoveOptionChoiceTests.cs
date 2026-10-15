using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class MoveOptionChoiceTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject hudObject;
    private GameObject popup;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static bool Equivalent(List<MoveOption> options) => (bool)Runtime("GameFlowSelectionController")
        .GetMethod("AreEquivalent", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { options });
    private static GameRules Rules(TestBoard board, GameState state, TurnManager turn) => new GameRules(
        state, turn, new GameEvents(), new YutThrower(new SequenceRandomProvider(0)),
        new StandardGetResult(), new AugmentPool(new SequenceRandomProvider(0)));
    private static object Selection(TestBoard board, GameRules rules, object hud, Action<MoveOption> apply) =>
        Activator.CreateInstance(Runtime("GameFlowSelectionController"), new object[]
        { null, board.State, rules, hud, null, Array.CreateInstance(Runtime("PieceView"), 0), new Func<int>(() => 0), apply, null });
    private static void Choose(object selection, List<MoveOption> options) => selection.GetType()
        .GetMethod("UseDestinationOptions", Private).Invoke(selection, new object[] { options });

    [TearDown]
    public void TearDown()
    {
        if (popup != null) UnityEngine.Object.DestroyImmediate(popup);
        if (hudObject != null) UnityEngine.Object.DestroyImmediate(hudObject);
    }

    [TestCase("Monk", YutResult.Gae, YutResult.Geol)]
    [TestCase("Veteran", YutResult.Geol, YutResult.Yut)]
    public void EqualForwardMovesAutomaticallyConsumeFirstStoredResult(string id, YutResult first, YutResult second)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0);
        board.State.AddAugment(AugmentFactories.Create()[id](0));
        var state = new GameState(board.State) { RemainingThrows = 0 };
        var turn = new TurnManager(state, 2); var rules = Rules(board, state, turn);
        state.RemainingThrows = 0;
        state.StoredResults.Add(first); state.StoredResults.Add(second);
        var options = rules.GetMoveOptions(piece.Id);
        Assert.That(options.Count, Is.EqualTo(2));
        Assert.That(options.All(o => o.ModifiedMoveCount == 3 && o.DestinationNodeId == 3), Is.True);
        Assert.That(Equivalent(options), Is.True);
        int applied = 0;
        Choose(Selection(board, rules, null, o =>
        { applied++; Assert.That(rules.MovePiece(piece.Id, o.ResultIndex, o.DestinationNodeId), Is.True); }), options);
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(piece.Node.Id, Is.EqualTo(3));
        CollectionAssert.AreEqual(new[] { second }, state.StoredResults);
    }

    [TestCase(0, YutResult.Do)]
    [TestCase(1, YutResult.BackDo)]
    public void ContrarianForwardMoveLetsPlayerChooseWhichResultToConsume(int index, YutResult remaining)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0);
        board.State.RestorePiece(piece.Id, PieceState.OnBoard, 3, 2);
        board.State.AddAugment(new ContrarianAugment(0));
        var state = new GameState(board.State);
        var turn = new TurnManager(state, 2); var rules = Rules(board, state, turn);
        state.RemainingThrows = 0;
        state.StoredResults.Add(YutResult.BackDo); state.StoredResults.Add(YutResult.Do);
        var options = rules.GetMoveOptions(piece.Id).Where(o => o.DestinationNodeId == 4).ToList();
        Assert.That(options.Count, Is.EqualTo(2));
        Assert.That(Equivalent(options), Is.False);
        hudObject = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameHudPanel_3Col.prefab"));
        var hud = hudObject.GetComponent(Runtime("GameHudPanel"));
        hud.GetType().GetMethod("Init").Invoke(hud, null);
        int applied = 0;
        Choose(Selection(board, rules, hud, o =>
        { applied++; Assert.That(rules.MovePiece(piece.Id, o.ResultIndex, o.DestinationNodeId), Is.True); }), options);
        var storedView = hud.GetType().GetField("storedResultView", Private).GetValue(hud);
        popup = (GameObject)storedView.GetType().GetField("moveOptionPopup", Private).GetValue(storedView);
        Assert.That(popup, Is.Not.Null);
        var buttons = popup.GetComponentsInChildren<Button>();
        CollectionAssert.AreEqual(new[] { "빽도", "도" }, buttons.Select(b => { var text = b.GetComponentInChildren(Runtime("TMPro.TMP_Text")); return (string)text.GetType().GetProperty("text").GetValue(text); }).ToArray());
        Assert.That(applied, Is.Zero);
        // HideChoices uses runtime Destroy. This EditMode fixture owns immediate cleanup instead.
        storedView.GetType().GetField("moveOptionPopup", Private).SetValue(storedView, null);
        buttons[index].onClick.Invoke();
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(piece.Node.Id, Is.EqualTo(4));
        CollectionAssert.AreEqual(new[] { remaining }, state.StoredResults);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContrarianBackDoKeepsTurnWithWaitingOrOnBoardPiece(bool onBoard)
    {
        var board = new TestBoard(); var piece = board.State.AddPiece(0, 0);
        if (onBoard) board.State.RestorePiece(piece.Id, PieceState.OnBoard, 3, 2);
        board.State.AddAugment(new ContrarianAugment(0));
        var state = new GameState(board.State); var turn = new TurnManager(state, 2);
        var rules = Rules(board, state, turn);
        rules.ApplyThrow(new[] { true, false, false, false }, YutResult.BackDo);
        Assert.That(turn.CurrentTeam, Is.Zero);
        Assert.That(rules.HasAnyStoredMove(), Is.True);
        Assert.That(rules.GetMoveOptions(piece.Id).Any(o => o.DestinationNodeId == (onBoard ? 4 : 1)), Is.True);
    }

    [Test]
    public void ExactArrivalAndFinishStillNeedChoice()
    {
        Assert.That(Equivalent(new List<MoveOption>
        {
            new MoveOption(0, YutResult.Do, 20, 1, false),
            new MoveOption(1, YutResult.Gae, 0, 2, true)
        }), Is.False);
        Assert.That(Equivalent(new List<MoveOption>
        {
            new MoveOption(0, YutResult.BackDo, 4, 1, false),
            new MoveOption(1, YutResult.Do, 4, 1, false)
        }), Is.False);
    }
}
