using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LobbyLifecycleTests
{
    private GameObject owner;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private Component Create(string type)
    {
        owner = new GameObject("LifecycleTest");
        owner.SetActive(false);
        return owner.AddComponent(RuntimeType(type));
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(owner);

    [Test]
    public void ProfilesSurviveInstallerClearingItsTemporaryList()
    {
        Component flow = Create("GameFlowController");
        Type profileType = RuntimeType("PlayerDisplayInfo");
        object profile = Activator.CreateInstance(profileType);
        profileType.GetField("nickname").SetValue(profile, "감자");
        profileType.GetField("iconId").SetValue(profile, "icon_004");
        profileType.GetField("team").SetValue(profile, 2);
        var source = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(profileType));
        source.Add(profile);
        flow.GetType().GetMethod("ConfigurePlayerProfiles").Invoke(flow, new object[] { source });
        source.Clear();
        profileType.GetField("nickname").SetValue(profile, "다른 값");
        var saved = (IList)Get(flow, "playerProfiles");
        Assert.That(saved.Count, Is.EqualTo(1));
        Assert.That(profileType.GetField("nickname").GetValue(saved[0]), Is.EqualTo("감자"));
        Assert.That(profileType.GetField("iconId").GetValue(saved[0]), Is.EqualTo("icon_004"));
        Assert.That(profileType.GetField("team").GetValue(saved[0]), Is.EqualTo(2));
        Assert.That(flow.GetType().GetMethod("PlayerName", Private).Invoke(flow, new object[] { 2 }), Is.EqualTo("감자"));
    }

    [Test]
    public void GameHudBindsAllFourHeaderProfileIcons()
    {
        owner = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameHudPanel_3Col.prefab"));
        Component hud = owner.GetComponent(RuntimeType("GameHudPanel"));
        hud.GetType().GetMethod("Init").Invoke(hud, null);
        hud.GetType().GetMethod("ConfigurePlayers").Invoke(hud, new object[] { 4, 4 });
        Type profileType = RuntimeType("PlayerDisplayInfo");
        var profiles = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(profileType));
        for (int team = 0; team < 4; team++)
        {
            object profile = Activator.CreateInstance(profileType);
            profileType.GetField("team").SetValue(profile, team);
            profileType.GetField("nickname").SetValue(profile, "닉네임" + team);
            profileType.GetField("iconId").SetValue(profile, "icon_001");
            profiles.Add(profile);
        }
        hud.GetType().GetMethod("ConfigurePlayerProfiles").Invoke(hud, new object[] { profiles });
        Component[] icons = owner.GetComponentsInChildren<Component>(true)
            .Where(component => component != null && component.name == "ProfileIcon" &&
                component.GetType().FullName == "UnityEngine.UI.Image").ToArray();
        Assert.That(icons.Length, Is.EqualTo(4));
        foreach (Component icon in icons)
        {
            Assert.That(icon.GetType().GetProperty("sprite").GetValue(icon), Is.Not.Null);
            Assert.That(icon.GetType().GetProperty("enabled").GetValue(icon), Is.True);
            Assert.That(icon.gameObject.activeInHierarchy, Is.True);
        }
    }

    [Test]
    public void LeavingClearsFinishedGameAndPreviousRoomState()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        Set(coordinator, "gameEnded", true);
        Set(coordinator, "returnToRoomRequested", true);
        Set(coordinator, "waitingForGameSession", true);
        Set(coordinator, "rewardAppliedThisGame", true);
        Set(coordinator, "pendingRoomCode", "OLD123");
        Set(coordinator, "lastRoomState", Activator.CreateInstance(RuntimeType("RoomStateMessage")));
        ((IDictionary)Get(coordinator, "playerTeams")).Add("p1", 0);
        Invoke(coordinator, "OnDisconnected", true);
        Assert.That(coordinator.GetType().GetProperty("CanReadyInCurrentRoom").GetValue(coordinator), Is.True);
        Assert.That(Get(coordinator, "lastRoomState"), Is.Null);
        Assert.That(Get(coordinator, "pendingRoomCode"), Is.Null);
        Assert.That(Get(coordinator, "returnToRoomRequested"), Is.False);
        Assert.That(Get(coordinator, "waitingForGameSession"), Is.False);
        Assert.That(Get(coordinator, "rewardAppliedThisGame"), Is.False);
        Assert.That(((IDictionary)Get(coordinator, "playerTeams")).Count, Is.Zero);
    }

    [Test]
    public void RoomBroadcastDuringLeaveDoesNotRestoreOldRoom()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        Set(coordinator, "leaveRequested", true);
        object previous = Activator.CreateInstance(RuntimeType("RoomStateMessage"));
        Set(coordinator, "lastRoomState", previous);
        object envelope = Activator.CreateInstance(RuntimeType("WsEnvelope"));
        RuntimeType("WsEnvelope").GetField("type").SetValue(envelope, Enum.Parse(RuntimeType("MessageType"), "ROOM_STATE"));
        Invoke(coordinator, "OnMessageReceived", envelope);
        Assert.That(Get(coordinator, "lastRoomState"), Is.SameAs(previous));
    }

    private Component CreateJoinCoordinator()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        var socketOwner = new GameObject("SocketTest"); socketOwner.transform.SetParent(owner.transform);
        socketOwner.SetActive(false);
        Set(coordinator, "webSocketClient", socketOwner.AddComponent(RuntimeType("WebSocketClient")));
        return coordinator;
    }
    private static object JoinEnvelope(string type, string payload) => RuntimeType("MessageSerializer")
        .GetMethod("Deserialize").Invoke(null, new object[] { "{\"type\":\"" + type + "\",\"seq\":0,\"payload\":" + payload + "}" });

    [Test]
    public void FailedJoinClearsPendingRoomBeforeRetry()
    {
        Component coordinator = CreateJoinCoordinator();
        Set(coordinator, "awaitingRoomJoin", true);
        Set(coordinator, "pendingRoomCode", "BAD");
        Set(coordinator, "pendingNickname", "감자");
        Set(coordinator, "lastRoomState", Activator.CreateInstance(RuntimeType("RoomStateMessage")));
        Invoke(coordinator, "OnMessageReceived", JoinEnvelope("ERROR", "{\"code\":\"ROOM_NOT_FOUND\",\"message\":\"없는 방입니다.\"}"));
        Assert.That(Get(coordinator, "awaitingRoomJoin"), Is.False);
        Assert.That(Get(coordinator, "pendingRoomCode"), Is.Null);
        Assert.That(Get(coordinator, "pendingNickname"), Is.Null);
        Assert.That(Get(coordinator, "lastRoomState"), Is.Null);
        Assert.That(Get(coordinator, "changingRoomSocket"), Is.False);
    }

    [Test]
    public void OldSocketCallbacksCannotClearNextJoin()
    {
        Component coordinator = CreateJoinCoordinator();
        Set(coordinator, "changingRoomSocket", true);
        Set(coordinator, "pendingRoomCode", "NEXT");
        Set(coordinator, "awaitingRoomJoin", true);
        Invoke(coordinator, "OnDisconnected", true);
        Invoke(coordinator, "OnSocketError", "old socket error");
        Invoke(coordinator, "OnMessageReceived", JoinEnvelope("ERROR", "{\"code\":\"ROOM_NOT_FOUND\"}"));
        Assert.That(Get(coordinator, "pendingRoomCode"), Is.EqualTo("NEXT"));
        Assert.That(Get(coordinator, "awaitingRoomJoin"), Is.True);
    }

    [Test]
    public void UnexpectedRoomBroadcastCannotAcceptPendingJoin()
    {
        Component coordinator = CreateJoinCoordinator();
        Set(coordinator, "awaitingRoomJoin", true);
        Set(coordinator, "pendingRoomCode", "NEXT");
        Invoke(coordinator, "OnMessageReceived", JoinEnvelope("ROOM_STATE", "{\"roomCode\":\"OLD\",\"members\":[]}"));
        Assert.That(Get(coordinator, "awaitingRoomJoin"), Is.True);
        Assert.That(Get(coordinator, "lastRoomState"), Is.Null);
    }

    [Test]
    public void ConnectionFailureDuringJoinClearsPendingRequest()
    {
        Component coordinator = CreateJoinCoordinator();
        Set(coordinator, "awaitingRoomJoin", true);
        Set(coordinator, "pendingRoomCode", "BAD");
        Invoke(coordinator, "OnSocketError", "connection failed");
        Assert.That(Get(coordinator, "awaitingRoomJoin"), Is.False);
        Assert.That(Get(coordinator, "pendingRoomCode"), Is.Null);
    }

    [Test]
    public async System.Threading.Tasks.Task LatestJoinWaitsForCleanupAndUsesItsOwnUrl()
    {
        Component coordinator = CreateJoinCoordinator();
        var closing = new System.Threading.Tasks.TaskCompletionSource<bool>();
        Set(coordinator, "roomSocketCleanupTask", closing.Task);
        Invoke(coordinator, "ConnectAndJoin", "OLD", "감자");
        Invoke(coordinator, "ConnectAndJoin", " NEXT ", "감자");
        Assert.That(Get(coordinator, "pendingRoomCode"), Is.Null);
        Component socket = (Component)Get(coordinator, "webSocketClient");
        closing.SetResult(true);
        await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();
        // 로그인 객체를 만들지 않아 실제 네트워크 연결은 실행되지 않는다.
        Assert.That(socket.GetType().GetProperty("Url").GetValue(socket), Is.EqualTo("ws://localhost:8080/ws/rooms/NEXT"));
        Assert.That(Get(coordinator, "awaitingRoomJoin"), Is.False);
    }
}
