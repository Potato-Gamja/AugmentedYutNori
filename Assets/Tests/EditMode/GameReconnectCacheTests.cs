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

}
