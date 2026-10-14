using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RethrowHudTests
{
    private GameObject flowObject;
    private GameObject hudObject;
    private Component flow;
    private Component hud;
    private GameRules rules;
    private Type flowType;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private void Set(string field, object value) => flowType.GetField(field, Private).SetValue(flow, value);

    [SetUp]
    public void SetUp()
    {
        var board = new TestBoard(); board.State.AddPiece(0, 0); board.State.AddPiece(100, 1);
        var state = new GameState(board.State);
        var turn = new TurnManager(state, 2);
        var events = new GameEvents();
        board.State.AddAugment(new RethrowAugment(0));
        rules = new GameRules(state, turn, events, new YutThrower(new SequenceRandomProvider(0)),
            new StandardGetResult(), new AugmentPool(new SequenceRandomProvider(0)));
        hudObject = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameHudPanel_3Col.prefab"));
        hud = hudObject.GetComponent(RuntimeType("GameHudPanel"));
        hud.GetType().GetMethod("Init").Invoke(hud, null);
        // 결과 연출은 제외하고 실제 HUD 버튼과 상태 전환을 확인한다.
        hud.GetType().GetField("resultRevealView", Private).SetValue(hud, null);
        var pieces = Array.CreateInstance(RuntimeType("PieceView"), 0);
        Func<int> currentTeam = () => turn.CurrentTeam;
        object presenter = Activator.CreateInstance(RuntimeType("GameFlowHudPresenter"),
            new object[] { hud, board.State, state, null, pieces, currentTeam });
        object selection = Activator.CreateInstance(RuntimeType("GameFlowSelectionController"),
            new object[] { null, board.State, rules, hud, null, pieces, currentTeam, null, presenter });
        flowObject = new GameObject("RethrowHudTestFlow"); flowObject.SetActive(false);
        flowType = RuntimeType("GameFlowController"); flow = flowObject.AddComponent(flowType);
        Set("setup", new GameSetup { teamCount = 2, piecesPerTeam = 1, localTeam = 0 });
        Set("gameState", state); Set("boardState", board.State); Set("turnManager", turn);
        Set("gameRules", rules); Set("hud", hud); Set("hudPresenter", presenter); Set("selectionController", selection);
        events.Thrown += outcome => flowType.GetMethod("OnThrown", Private).Invoke(flow, new object[] { outcome });
        flowType.GetMethod("ChangeState", Private).Invoke(flow,
            new[] { Enum.Parse(RuntimeType("GameFlowState"), "Throw") });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(flowObject);
        UnityEngine.Object.DestroyImmediate(hudObject);
    }
    private object Button(string field) => hud.GetType().GetField(field, Private).GetValue(hud);
    private static bool Interactable(object button) => (bool)button.GetType().GetProperty("interactable").GetValue(button);
    private static bool Visible(object button) => ((Component)button).gameObject.activeSelf;

    [Test]
    public void FirstBackDo_DisablesNormalThrowAndEnablesRethrow()
    {
        rules.ApplyThrow(new[] { true, false, false, false }, YutResult.BackDo);
        Assert.That(flowType.GetProperty("CurrentState").GetValue(flow).ToString(), Is.EqualTo("Move"));
        Assert.That(Interactable(Button("throwButton")), Is.False);
        Assert.That(Visible(Button("rethrowButton")), Is.True);
        Assert.That(Interactable(Button("rethrowButton")), Is.True);
    }
    [Test]
    public void FirstDo_StillEnablesRethrow()
    {
        rules.ApplyThrow(new[] { false, true, false, false }, YutResult.Do);
        Assert.That(Interactable(Button("throwButton")), Is.False);
        Assert.That(Interactable(Button("rethrowButton")), Is.True);
    }
    [Test]
    public void Yut_EnablesExtraThrowInsteadOfRethrow()
    {
        rules.ApplyThrow(new[] { true, true, true, true }, YutResult.Yut);
        Assert.That(flowType.GetProperty("CurrentState").GetValue(flow).ToString(), Is.EqualTo("Throw"));
        Assert.That(Interactable(Button("throwButton")), Is.True);
        Assert.That(Visible(Button("rethrowButton")), Is.False);
    }
}
