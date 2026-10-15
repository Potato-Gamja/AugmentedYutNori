using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameReconnectCacheTests
{
    private object cache;
    private object data;
    private string key;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        key = "ReconnectTest." + Guid.NewGuid();
        cache = Activator.CreateInstance(RuntimeType("GameReconnectCache"), key);
        data = Activator.CreateInstance(RuntimeType("GameReconnectData"));
        Set(data, "playerId", "p1");
        Set(data, "serverUrl", "http://localhost:8080");
        object setup = Activator.CreateInstance(RuntimeType("GameStartMessage"));
        Set(setup, "roomCode", "ABC123");
        Set(setup, "teamCount", 2);
        Set(setup, "piecesPerTeam", 4);
        Set(setup, "localTeam", 0);
        Set(setup, "boardLayoutId", "default");
        Set(data, "setup", setup);
        object profile = Activator.CreateInstance(RuntimeType("PlayerDisplayInfo"));
        Set(profile, "playerId", "p1"); Set(profile, "team", 0);
        Set(profile, "nickname", "감자"); Set(profile, "iconId", "icon_004");
        ((IList)data.GetType().GetField("profiles").GetValue(data)).Add(profile);
    }
    [TearDown] public void TearDown() => Call(cache, "Clear");

    [Test]
    public void SavedGameRoundTripKeepsSetupAndProfile()
    {
        Call(cache, "Save", data);
        object loaded = Call(cache, "Load", "p1", "http://localhost:8080/");
        Assert.That(loaded, Is.Not.Null);
        object profile = ((IList)loaded.GetType().GetField("profiles").GetValue(loaded))[0];
        Assert.That(profile.GetType().GetField("iconId").GetValue(profile), Is.EqualTo("icon_004"));
        Assert.That(profile.GetType().GetField("nickname").GetValue(profile), Is.EqualTo("감자"));
    }
    [TestCase("p2", "http://localhost:8080")]
    [TestCase("p1", "http://localhost:8081")]
    public void OtherAccountOrServerCannotUseCachedGame(string player, string server)
    {
        Call(cache, "Save", data);
        Assert.That(Call(cache, "Load", player, server), Is.Null);
    }
    [TestCase("PLAYING", 0, true, true)]
    [TestCase("AUGMENT_SELECT", 0, true, true)]
    [TestCase("AUGMENT_SELECT", 0, false, true)]
    [TestCase("AUGMENT_SELECT", 1, false, false)]
    [TestCase("ENDED", 0, false, false)]
    [TestCase("WAITING", 0, false, false)]
    [TestCase(null, 0, false, false)]
    [TestCase("ENDED", 0, true, false)]
    [TestCase("PLAYING", 1, true, false)]
    [TestCase("PLAYING", 0, false, false)]
    public void OnlyActiveSnapshotWithOwnTeamCanRestore(string phase, int team, bool hasPiece, bool expected)
    {
        object snapshot = Activator.CreateInstance(RuntimeType("GameSnapshotMessage"));
        Set(snapshot, "phase", phase); Set(snapshot, "myTeam", team);
        if (hasPiece)
        {
            object piece = Activator.CreateInstance(RuntimeType("PieceSnapshot"));
            Set(piece, "team", team);
            ((IList)snapshot.GetType().GetField("pieces").GetValue(snapshot)).Add(piece);
        }
        Assert.That(Call(data, "CanRestore", snapshot), Is.EqualTo(expected));
    }
    [Test]
    public void MissingSnapshotOrPieceListCannotRestore()
    {
        Assert.That(Call(data, "CanRestore", new object[] { null }), Is.False);
        object snapshot = Activator.CreateInstance(RuntimeType("GameSnapshotMessage"));
        Set(snapshot, "phase", "AUGMENT_SELECT");
        Set(snapshot, "myTeam", 0);
        Set(snapshot, "pieces", null);
        Assert.That(Call(data, "CanRestore", snapshot), Is.False);
    }

    [Test]
    public void EmptySelectionSnapshotCompletesReconnectAndKeepsCachedGame()
    {
        var owner = new GameObject("SelectionReconnectTest");
        owner.SetActive(false);
        try
        {
            Call(cache, "Save", data);
            Component coordinator = owner.AddComponent(RuntimeType("LobbyNetworkCoordinator"));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            coordinator.GetType().GetField("reconnecting", flags).SetValue(coordinator, true);
            coordinator.GetType().GetField("reconnectData", flags).SetValue(coordinator, data);
            coordinator.GetType().GetField("reconnectCache", flags).SetValue(coordinator, cache);
            Type waiterType = typeof(System.Threading.Tasks.TaskCompletionSource<>).MakeGenericType(RuntimeType("WsEnvelope"));
            object waiter = Activator.CreateInstance(waiterType);
            coordinator.GetType().GetField("reconnectSnapshot", flags).SetValue(coordinator, waiter);
            object envelope = RuntimeType("MessageSerializer").GetMethod("Deserialize").Invoke(null, new object[]
            {
                "{\"type\":\"GAME_SNAPSHOT\",\"seq\":0,\"payload\":{\"phase\":\"AUGMENT_SELECT\",\"myTeam\":0,\"pieces\":[]}}"
            });
            coordinator.GetType().GetMethod("OnMessageReceived", flags).Invoke(coordinator, new[] { envelope });
            var task = (System.Threading.Tasks.Task)waiterType.GetProperty("Task").GetValue(waiter);
            Assert.That(task.Status, Is.EqualTo(System.Threading.Tasks.TaskStatus.RanToCompletion));
            Assert.That(coordinator.GetType().GetField("terminalReconnectFailure", flags).GetValue(coordinator), Is.False);
            Assert.That(coordinator.GetType().GetField("waitingForGameSession", flags).GetValue(coordinator), Is.True);
            var pending = (IList)coordinator.GetType().GetField("pendingGameMessages", flags).GetValue(coordinator);
            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(Call(cache, "Load", "p1", "http://localhost:8080"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void ClearAndMalformedCacheDoNotOfferGame()
    {
        Call(cache, "Save", data); Call(cache, "Clear");
        Assert.That(Call(cache, "Load", "p1", "http://localhost:8080"), Is.Null);
        PlayerPrefs.SetString(key, "broken json");
        Assert.That(Call(cache, "Load", "p1", "http://localhost:8080"), Is.Null);
    }
    [Test]
    public void ClosingPanelCancelsSnapshotWait()
    {
        var owner = new GameObject("CancelReconnectTest");
        owner.SetActive(false);
        try
        {
            Component coordinator = owner.AddComponent(RuntimeType("LobbyNetworkCoordinator"));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type waiterType = typeof(System.Threading.Tasks.TaskCompletionSource<>).MakeGenericType(RuntimeType("WsEnvelope"));
            object waiter = Activator.CreateInstance(waiterType);
            coordinator.GetType().GetField("reconnecting", flags).SetValue(coordinator, true);
            coordinator.GetType().GetField("reconnectSnapshot", flags).SetValue(coordinator, waiter);
            Call(coordinator, "CancelReconnect");
            var task = (System.Threading.Tasks.Task)waiterType.GetProperty("Task").GetValue(waiter);
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(coordinator.GetType().GetField("reconnectCanceled", flags).GetValue(coordinator), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase("REMOVED_FROM_GAME", true)]
    [TestCase("GAME_ALREADY_ENDED", true)]
    [TestCase("GAME_NOT_FOUND", true)]
    [TestCase("PLAYER_NOT_IN_GAME", true)]
    [TestCase("NOT_YOUR_TURN", false)]
    [TestCase(null, false)]
    public void ClassifiesOnlyTerminalReconnectCodes(string code, bool expected)
    {
        Assert.That(RuntimeType("ReconnectFailure").GetMethod("IsTerminal").Invoke(null, new object[] { code }), Is.EqualTo(expected));
    }

    [TestCase("REMOVED_FROM_GAME")]
    [TestCase("GAME_ALREADY_ENDED")]
    [TestCase("GAME_NOT_FOUND")]
    [TestCase("PLAYER_NOT_IN_GAME")]
    public void LobbyTerminalErrorClearsCacheAndFailsPendingAttempt(string code)
    {
        Call(cache, "Save", data);
        var owner = new GameObject("LobbyTerminalErrorTest"); owner.SetActive(false);
        try
        {
            Component coordinator = owner.AddComponent(RuntimeType("LobbyNetworkCoordinator"));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type waiterType = typeof(System.Threading.Tasks.TaskCompletionSource<>).MakeGenericType(RuntimeType("WsEnvelope"));
            object waiter = Activator.CreateInstance(waiterType);
            coordinator.GetType().GetField("reconnecting", flags).SetValue(coordinator, true);
            coordinator.GetType().GetField("reconnectSnapshot", flags).SetValue(coordinator, waiter);
            coordinator.GetType().GetField("reconnectCache", flags).SetValue(coordinator, cache);
            object error = Activator.CreateInstance(RuntimeType("ErrorMessage"));
            Set(error, "code", code); Set(error, "message", "복귀 거절");
            Type serializer = RuntimeType("MessageSerializer");
            MethodInfo serialize = serializer.GetMethods().First(method => method.Name == "Serialize" && method.IsGenericMethod);
            string json = (string)serialize.MakeGenericMethod(error.GetType()).Invoke(null,
                new[] { Enum.Parse(RuntimeType("MessageType"), "ERROR"), error, (object)0L });
            object envelope = serializer.GetMethod("Deserialize").Invoke(null, new object[] { json });
            coordinator.GetType().GetMethod("OnMessageReceived", flags).Invoke(coordinator, new[] { envelope });
            Assert.That(Call(cache, "Load", "p1", "http://localhost:8080"), Is.Null);
            Assert.That(coordinator.GetType().GetField("terminalReconnectFailure", flags).GetValue(coordinator), Is.True);
            var task = (System.Threading.Tasks.Task)waiterType.GetProperty("Task").GetValue(waiter);
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.InnerException.Message, Is.EqualTo("복귀 거절"));
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

}
