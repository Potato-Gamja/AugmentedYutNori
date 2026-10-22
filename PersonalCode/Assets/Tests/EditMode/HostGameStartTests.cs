using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HostGameStartTests
{
    private readonly List<GameObject> owners = new();
    private Component panel, controller, coordinator;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static object Public(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private Component Create(string name)
    {
        var go = new GameObject(name + "HostTest"); go.SetActive(false); owners.Add(go);
        return go.AddComponent(TypeOf(name));
    }
    private static object Room(int count, string host = "p0", bool ready = true, string status = "WAITING")
    {
        object room = Activator.CreateInstance(TypeOf("RoomStateMessage"));
        room.GetType().GetField("roomCode").SetValue(room, "HOST01");
        room.GetType().GetField("hostPlayerId").SetValue(room, host);
        room.GetType().GetField("status").SetValue(room, status);
        var members = (IList)room.GetType().GetField("members").GetValue(room);
        for (int team = 0; team < count; team++)
        {
            object player = Activator.CreateInstance(TypeOf("RoomPlayer"));
            player.GetType().GetField("playerId").SetValue(player, "p" + team);
            player.GetType().GetField("nickname").SetValue(player, "참가자" + team);
            player.GetType().GetField("team").SetValue(player, team);
            player.GetType().GetField("ready").SetValue(player, ready);
            members.Add(player);
        }
        return room;
    }
    private Component StartButton => (Component)Get(panel, "startGameButton");
    private static bool Interactable(Component button) => (bool)button.GetType().GetProperty("interactable").GetValue(button);
    private void ClickStart()
    {
        object click = StartButton.GetType().GetProperty("onClick").GetValue(StartButton);
        click.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(click, null);
    }
    private void Receive(object room) => Public(controller, "ReceiveRoomState", room);
    private bool CanRequest => (bool)coordinator.GetType().GetProperty("CanStartGameInCurrentRoom").GetValue(coordinator);

    [SetUp]
    public void SetUp()
    {
        var go = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UIv2/Panels/RoomPanel.prefab"));
        owners.Add(go); panel = go.GetComponent(TypeOf("RoomPanel")); Public(panel, "Init");
        controller = Create("LobbyController");
        Set(controller, "roomPanel", panel); Set(controller, "localPlayerId", "p0");
        Component manager = Create("UIManager");
        ((IDictionary)Get(manager, "_instances")).Add(TypeOf("RoomPanel"), panel);
        Set(controller, "uiManager", manager);
        coordinator = Create("LobbyNetworkCoordinator");
        object identity = Activator.CreateInstance(TypeOf("PlayerIdentity"), new object[] { "HostTest." + Guid.NewGuid() });
        identity.GetType().GetProperty("PlayerId").SetValue(identity, "p0");
        Set(coordinator, "playerIdentity", identity);
        Set(coordinator, "lobbyController", controller);
    }
    [TearDown]
    public void TearDown()
    {
        foreach (GameObject owner in owners) if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        owners.Clear();
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public void RoomStateConnectsHostButton_AndNeverAutomaticallyStarts(int count, bool expected)
    {
        int requests = 0;
        Action callback = () => requests++;
        controller.GetType().GetEvent("StartGameRequested").AddEventHandler(controller, callback);
        object room = Room(count); Set(coordinator, "lastRoomState", room); Receive(room);
        Assert.That(StartButton.gameObject.activeSelf, Is.EqualTo(expected));
        Assert.That(CanRequest, Is.EqualTo(expected));
        Assert.That(requests, Is.Zero);
        if (!expected) return;
        ClickStart(); ClickStart();
        Assert.That(requests, Is.EqualTo(1));
        Assert.That(Interactable(StartButton), Is.False);
        Public(controller, "ReceiveError", "시작 거절");
        Assert.That(Interactable(StartButton), Is.True);
    }

    [Test]
    public void HostTransferUsesServerPlayerId_NotFirstTeam()
    {
        Receive(Room(3, "p1"));
        Assert.That(StartButton.gameObject.activeSelf, Is.False);
        Receive(Room(3, "p0"));
        Assert.That(StartButton.gameObject.activeSelf, Is.True);
        Receive(Room(3, null));
        Assert.That(StartButton.gameObject.activeSelf, Is.False);
    }

    [TestCase(false, "WAITING")]
    [TestCase(true, "PLAYING")]
    public void NotReadyOrPlayingBlocksStart(bool ready, string status)
    {
        object room = Room(3, ready: ready, status: status);
        Set(coordinator, "lastRoomState", room); Receive(room);
        Assert.That(StartButton.gameObject.activeSelf, Is.False);
        Assert.That(CanRequest, Is.False);
    }

    [Test]
    public void DisconnectedPlayerBlocksStart_UntilReconnected()
    {
        object room = Room(4); Set(coordinator, "lastRoomState", room); Receive(room);
        var disconnected = (HashSet<string>)Get(coordinator, "reconnectingRoomPlayers");
        disconnected.Add("p2"); Public(controller, "ReceiveRoomConnections", disconnected);
        Assert.That(StartButton.gameObject.activeSelf, Is.False);
        Assert.That(CanRequest, Is.False);
        disconnected.Clear(); Public(controller, "ReceiveRoomConnections", disconnected);
        Assert.That(StartButton.gameObject.activeSelf, Is.True);
        Assert.That(CanRequest, Is.True);
    }

    [Test]
    public void MissingSocketRestoresInputAfterRequestFailure()
    {
        var handler = (Action)Delegate.CreateDelegate(typeof(Action), coordinator,
            coordinator.GetType().GetMethod("OnStartGameRequested", Private));
        controller.GetType().GetEvent("StartGameRequested").AddEventHandler(controller, handler);
        object room = Room(2); Set(coordinator, "lastRoomState", room); Receive(room);
        ClickStart();
        Assert.That(Interactable(StartButton), Is.True);
        object status = Get(panel, "statusText");
        Assert.That(status.GetType().GetProperty("text").GetValue(status), Is.EqualTo("서버에 연결되어 있지 않습니다."));
    }

    [Test]
    public void LatestRoomStateReplacesOldTeamAssignmentsBeforeGameStart()
    {
        object room = Room(3);
        var members = (IList)room.GetType().GetField("members").GetValue(room);
        members[1].GetType().GetField("team").SetValue(members[1], 2);
        members[2].GetType().GetField("team").SetValue(members[2], 3);
        Call(coordinator, "CachePlayerTeams", room);
        members[1].GetType().GetField("team").SetValue(members[1], 1);
        members[2].GetType().GetField("team").SetValue(members[2], 2);
        Call(coordinator, "CachePlayerTeams", room);
        var teams = (IDictionary)Get(coordinator, "playerTeams");
        Assert.That(teams["p1"], Is.EqualTo(1));
        Assert.That(teams["p2"], Is.EqualTo(2));
        var profiles = (IList)Get(coordinator, "playerProfiles");
        Assert.That(profiles.Count, Is.EqualTo(3));
        Assert.That(profiles[2].GetType().GetField("team").GetValue(profiles[2]), Is.EqualTo(2));
    }

    private string SlotName(int slot)
    {
        object text = ((Array)Get(panel, "playerNameTexts")).GetValue(slot);
        return (string)text.GetType().GetProperty("text").GetValue(text);
    }
    private Color SlotColor(int slot)
    {
        object image = ((Array)Get(panel, "playerSlotBackgrounds")).GetValue(slot);
        return (Color)image.GetType().GetProperty("color").GetValue(image);
    }
    private static Color ExpectedTeamColor(int team) => ((Color[])TypeOf("RoomPanel")
        .GetField("TeamColors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))[team];

    [Test]
    public void HostLeavesAndNewPlayerFillsOldTeam_PreviewMatchesGameStartWithoutChangingServerTeams()
    {
        object room = Room(4);
        var members = (IList)room.GetType().GetField("members").GetValue(room);
        members.RemoveAt(0);
        room.GetType().GetField("hostPlayerId").SetValue(room, "p1");
        Receive(room);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(SlotName(i), Is.EqualTo("참가자" + (i + 1)));
            Assert.That(SlotColor(i), Is.EqualTo(ExpectedTeamColor(i)));
            Assert.That(members[i].GetType().GetField("team").GetValue(members[i]), Is.EqualTo(i + 1));
        }
        object otherRoom = Room(1);
        object newcomer = ((IList)otherRoom.GetType().GetField("members").GetValue(otherRoom))[0];
        newcomer.GetType().GetField("playerId").SetValue(newcomer, "p4");
        newcomer.GetType().GetField("nickname").SetValue(newcomer, "새 참가자");
        members.Add(newcomer); // 서버는 비어 있는 team 0을 주지만, 시작할 때 마지막 team 3으로 배정한다.
        Receive(room);
        Assert.That(SlotName(0), Is.EqualTo("참가자1"));
        Assert.That(SlotName(3), Is.EqualTo("새 참가자"));
        Assert.That(SlotColor(3), Is.EqualTo(ExpectedTeamColor(3)));
        Assert.That(newcomer.GetType().GetField("team").GetValue(newcomer), Is.EqualTo(0));
        var names = Enumerable.Range(0, 4).Select(SlotName).ToArray();
        var colors = Enumerable.Range(0, 4).Select(SlotColor).ToArray();
        for (int i = 0; i < 4; i++) members[i].GetType().GetField("team").SetValue(members[i], i);
        room.GetType().GetField("status").SetValue(room, "PLAYING");
        Receive(room);
        Assert.That(Enumerable.Range(0, 4).Select(SlotName), Is.EqualTo(names));
        Assert.That(Enumerable.Range(0, 4).Select(SlotColor), Is.EqualTo(colors));
    }

    [Test]
    public void PlayingRoomUsesActualPlayerColorsInsteadOfFixedSeatColors()
    {
        object room = Room(2, status: "PLAYING");
        var members = (IList)room.GetType().GetField("members").GetValue(room);
        members[0].GetType().GetField("team").SetValue(members[0], 3);
        members[1].GetType().GetField("team").SetValue(members[1], 1);
        Receive(room);
        Assert.That(SlotName(0), Is.EqualTo("참가자1"));
        Assert.That(SlotColor(0), Is.EqualTo(ExpectedTeamColor(1)));
        Assert.That(SlotColor(1), Is.EqualTo(ExpectedTeamColor(3)));
    }

    [Test]
    public void ProtocolReadsHostAndSerializesStartGameWithoutPayloadFields()
    {
        Type serializer = TypeOf("MessageSerializer");
        object message = serializer.GetMethod("Deserialize").Invoke(null, new object[]
        {
            "{\"type\":\"ROOM_STATE\",\"payload\":{\"hostPlayerId\":\"p2\",\"members\":[]}}"
        });
        object room = serializer.GetMethod("ReadPayload").MakeGenericMethod(TypeOf("RoomStateMessage")).Invoke(null, new[] { message });
        Assert.That(room.GetType().GetField("hostPlayerId").GetValue(room), Is.EqualTo("p2"));
        object start = Enum.Parse(TypeOf("MessageType"), "START_GAME");
        string json = (string)serializer.GetMethods().Single(method => method.Name == "Serialize" && !method.IsGenericMethod)
            .Invoke(null, new object[] { start, 0L });
        Assert.That(json, Does.Contain("\"type\":\"START_GAME\""));
        Assert.That(json, Does.Contain("\"payload\":{}"));
    }
}
