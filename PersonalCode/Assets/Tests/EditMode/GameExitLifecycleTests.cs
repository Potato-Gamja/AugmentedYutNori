using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class GameExitLifecycleTests
{
    private GameObject owner;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private Component Create(string name)
    {
        owner = new GameObject("GameExitTest");
        owner.SetActive(false);
        return owner.AddComponent(RuntimeType(name));
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(owner);

    [Test]
    public void TeamsCanBeRegisteredBeforeStart()
    {
        Component flow = Create("GameFlowController");
        var setup = new GameSetup { teamCount = 4 };
        flow.GetType().GetMethod("Configure", new[] { typeof(GameSetup) }).Invoke(flow, new object[] { setup });
        for (int team = 0; team < 4; team++)
            flow.GetType().GetMethod("RegisterPlayerTeam").Invoke(flow, new object[] { "p" + team, team });
        var teams = (IDictionary)flow.GetType().GetField("playerTeams", Private).GetValue(flow);
        Assert.That(teams.Count, Is.EqualTo(4));
        Assert.That(teams["p1"], Is.EqualTo(1));
    }

    [TestCase("HandleMainMenu", "MainMenuClicked")]
    [TestCase("HandleReturnToRoom", "ReturnToRoomClicked")]
    public void ResultButtonSendsOnlyOneExit(string handler, string eventName)
    {
        Component popup = Create("ResultPopup");
        int calls = 0;
        popup.GetType().GetEvent(eventName).AddEventHandler(popup, (Action)(() => calls++));
        Call(popup, handler);
        Call(popup, handler);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void LateGameEndCannotChangeStateAfterExitStarts()
    {
        Component flow = Create("GameFlowController");
        object previous = flow.GetType().GetField("currentState", Private).GetValue(flow);
        Assert.That(Call(flow, "BeginLeavingGame"), Is.True);
        Assert.That(Call(flow, "BeginLeavingGame"), Is.False);
        Call(flow, "OnGameEnded", 0);
        Assert.That(flow.GetType().GetField("currentState", Private).GetValue(flow), Is.EqualTo(previous));
        Assert.That(flow.GetType().GetField("pendingWinnerTeam", Private).GetValue(flow), Is.Null);
    }

    [Test]
    public void GameEndWaitsForMoveCompletion()
    {
        Component flow = Create("GameFlowController");
        Set(flow, "isMoving", true);
        object previous = flow.GetType().GetField("currentState", Private).GetValue(flow);
        Call(flow, "OnGameEnded", 0);
        Assert.That(flow.GetType().GetField("currentState", Private).GetValue(flow), Is.EqualTo(previous));
        Assert.That(flow.GetType().GetField("pendingWinnerTeam", Private).GetValue(flow), Is.EqualTo(0));
    }

    [Test]
    public void DuplicateGameEndDoesNotOpenResultsAgain()
    {
        Component flow = Create("GameFlowController");
        var field = flow.GetType().GetField("currentState", Private);
        field.SetValue(flow, Enum.Parse(field.FieldType, "Finish"));
        Call(flow, "OnGameEnded", 0);
        Assert.That(flow.GetType().GetField("pendingWinnerTeam", Private).GetValue(flow), Is.Null);
        Assert.That(field.GetValue(flow).ToString(), Is.EqualTo("Finish"));
    }

    [Test]
    public void FinishedPieceCompletesBeforeItIsHidden()
    {
        Component view = Create("PieceView");
        owner.AddComponent<SpriteRenderer>();
        owner.SetActive(true);
        bool completed = false;
        var routine = (IEnumerator)Call(view, "MoveRoutine", Array.Empty<Vector3>(), true,
            (Action)(() => { completed = true; Assert.That(owner.activeSelf, Is.True); }));
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(completed, Is.True);
        Assert.That(owner.activeSelf, Is.False);
        Assert.That(view.GetType().GetProperty("IsMoving").GetValue(view), Is.False);
    }

    [Test]
    public async Task SocketCloseDoesNotWaitForever()
    {
        var pending = new TaskCompletionSource<bool>();
        MethodInfo wait = RuntimeType("WebSocketClient").GetMethod("WaitForCloseAsync", BindingFlags.Static | BindingFlags.NonPublic);
        bool closed = await (Task<bool>)wait.Invoke(null, new object[] { pending.Task, 10 });
        Assert.That(closed, Is.False);
        pending.SetResult(true);
        Assert.That(await (Task<bool>)wait.Invoke(null, new object[] { Task.CompletedTask, 10 }), Is.True);
    }
}
