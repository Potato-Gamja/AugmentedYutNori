using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RoomReconnectStatusTests
{
    private readonly List<GameObject> owners = new();
    private Component panel;
    private Component coordinator;
    private object room;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private Component Create(string name)
    {
        var go = new GameObject(name + "Test"); go.SetActive(false); owners.Add(go);
        return go.AddComponent(RuntimeType(name));
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static object Envelope(string type, string payload) => RuntimeType("MessageSerializer").GetMethod("Deserialize")
        .Invoke(null, new object[] { "{\"type\":\"" + type + "\",\"seq\":0,\"payload\":" + payload + "}" });
    private void Message(string type, string payload) => Call(coordinator, "OnMessageReceived", Envelope(type, payload));
    private string Status(int index)
    {
        object text = ((Array)Get(panel, "playerStatusTexts")).GetValue(index);
        return (string)text.GetType().GetProperty("text").GetValue(text);
    }
    private void ShowRoom() => panel.GetType().GetMethod("ShowRoom").Invoke(panel, new[] { room, "p0" });

    [SetUp]
    public void SetUp()
    {
        var go = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Panels/RoomPanel.prefab"));
        owners.Add(go); panel = go.GetComponent(RuntimeType("RoomPanel"));
        coordinator = Create("LobbyNetworkCoordinator");
        Component controller = Create("LobbyController"); Set(controller, "roomPanel", panel);
        Set(coordinator, "lobbyController", controller);
        object envelope = Envelope("ROOM_STATE", "{\"roomCode\":\"ROOM1\",\"members\":[{\"playerId\":\"p0\",\"nickname\":\"감자\",\"team\":0,\"ready\":false},{\"playerId\":\"p1\",\"nickname\":\"친구\",\"team\":2,\"ready\":true}]}");
        room = RuntimeType("MessageSerializer").GetMethod("ReadPayload").MakeGenericMethod(RuntimeType("RoomStateMessage"))
            .Invoke(null, new[] { envelope });
        Set(coordinator, "lastRoomState", room); ShowRoom();
    }
    [TearDown] public void TearDown()
    {
        foreach (var go in owners) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        owners.Clear();
    }

    [Test]
    public void DisconnectMarksPlayerByIdInsteadOfTeamSlot()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        Assert.That(Status(0), Is.EqualTo("대기 중"));
        Assert.That(Status(1), Is.EqualTo("재연결 대기 중..."));
        Assert.That(Status(2), Is.EqualTo("빈 자리"));
    }
    [Test]
    public void RoomRefreshPreservesWaitingStatus()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        ShowRoom();
        Assert.That(Status(1), Is.EqualTo("재연결 대기 중..."));
    }
    [Test]
    public void ReconnectRestoresServerReadyStatus()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        Message("PLAYER_RECONNECTED", "{\"playerId\":\"p1\"}");
        Assert.That(Status(1), Is.EqualTo("준비 완료"));
    }
    [Test]
    public void MemberRemovalLeavesAnEmptySlot()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        ((IList)room.GetType().GetField("members").GetValue(room)).RemoveAt(1);
        ShowRoom();
        Assert.That(Status(1), Is.EqualTo("빈 자리"));
    }
    [Test]
    public void RoomChangeAndSessionClearDiscardWaitingStatus()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        room.GetType().GetField("roomCode").SetValue(room, "ROOM2"); ShowRoom();
        Assert.That(Status(1), Is.EqualTo("준비 완료"));
        Call(coordinator, "ClearRoomSessionState");
        object waiting = Get(coordinator, "reconnectingRoomPlayers");
        Assert.That(waiting.GetType().GetProperty("Count").GetValue(waiting), Is.EqualTo(0));
    }
    [Test]
    public void UnknownPlayerAndLateLeaveMessagesDoNotMarkSlots()
    {
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"unknown\",\"graceSec\":30}");
        Set(coordinator, "leaveRequested", true);
        Message("PLAYER_DISCONNECTED", "{\"playerId\":\"p1\",\"graceSec\":30}");
        Assert.That(Status(1), Is.EqualTo("준비 완료"));
    }
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void UnequippedPlayersUseLockedIconRegardlessOfSeat(string iconId)
    {
        var members = (IList)room.GetType().GetField("members").GetValue(room);
        members.Clear();
        room.GetType().GetField("status").SetValue(room, "WAITING");
        for (int seat = 0; seat < 4; seat++)
        {
            var player = Activator.CreateInstance(RuntimeType("RoomPlayer"));
            player.GetType().GetField("playerId").SetValue(player, "p" + seat);
            player.GetType().GetField("nickname").SetValue(player, "참가자" + seat);
            player.GetType().GetField("team").SetValue(player, 3 - seat);
            player.GetType().GetField("iconId").SetValue(player, iconId);
            members.Add(player);
        }
        ShowRoom();
        var icons = (UnityEngine.UI.Image[])Get(panel, "playerProfileIcons");
        var defaultIcon = Resources.Load<Sprite>("ProfileIcons/profile_px_locked");
        Assert.That(defaultIcon, Is.Not.Null);
        foreach (var icon in icons)
        {
            Assert.That(icon.sprite, Is.EqualTo(defaultIcon));
            Assert.That(icon.enabled, Is.True);
        }
        members[1].GetType().GetField("iconId").SetValue(members[1], "icon_002");
        members.RemoveAt(3);
        ShowRoom();
        Assert.That(icons[1].sprite, Is.EqualTo(Resources.Load<Sprite>("ProfileIcons/icon_002")));
        Assert.That(icons[3].sprite, Is.Null);
        Assert.That(icons[3].enabled, Is.False);
    }

}

