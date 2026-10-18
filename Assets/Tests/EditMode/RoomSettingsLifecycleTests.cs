using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RoomSettingsLifecycleTests
{
    private readonly List<GameObject> objects = new();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private Component Create(string name)
    {
        var owner = new GameObject(name + "Test");
        owner.SetActive(false);
        objects.Add(owner);
        return owner.AddComponent(RuntimeType(name));
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    [TearDown] public void TearDown()
    {
        foreach (GameObject owner in objects) if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        objects.Clear();
    }

    [Test]
    public void DestroyedLobbyDoesNotReceiveLateSocketCallbacks()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        Component controller = Create("LobbyController");
        Set(coordinator, "lobbyController", controller);
        UnityEngine.Object.DestroyImmediate(controller.gameObject);
        Assert.DoesNotThrow(() => Call(coordinator, "OnSocketError", "late error"));
        Assert.DoesNotThrow(() => Call(coordinator, "OnDisconnected", true));
    }

    [Test]
    public void UnbindClearsDestroyedLobbyReference()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        Component controller = Create("LobbyController");
        Set(coordinator, "lobbyController", controller);
        UnityEngine.Object.DestroyImmediate(controller.gameObject);
        Call(coordinator, "UnbindLobbyController");
        Assert.That(ReferenceEquals(Get(coordinator, "lobbyController"), null), Is.True);
    }

    [Test]
    public void CurrentLobbyStillReceivesSocketErrors()
    {
        Component coordinator = Create("LobbyNetworkCoordinator");
        Component controller = Create("LobbyController");
        GameObject room = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Panels/RoomPanel.prefab"));
        objects.Add(room);
        Component panel = room.GetComponent(RuntimeType("RoomPanel"));
        Set(controller, "roomPanel", panel);
        Set(coordinator, "lobbyController", controller);
        Call(coordinator, "OnSocketError", "test error");
        object status = Get(panel, "statusText");
        Assert.That(status.GetType().GetProperty("text").GetValue(status), Is.EqualTo("서버 연결 오류: test error"));
    }

    [Test]
    public void RoomSettingsButtonOpensExistingSettingsPanel()
    {
        GameObject uiRoot = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UIRoot.prefab"));
        objects.Add(uiRoot);
        Component manager = uiRoot.GetComponentInChildren(RuntimeType("UIManager"), true);
        Call(manager, "Awake");
        Component controller = Create("LobbyController");
        Set(controller, "uiManager", manager);
        var get = manager.GetType().GetMethod("Get").MakeGenericMethod(RuntimeType("RoomPanel"));
        Component panel = (Component)get.Invoke(manager, null);
        MethodInfo settingsHandler = controller.GetType().GetMethod("OnSettingsRequested", Private);
        var handler = (Action)Delegate.CreateDelegate(typeof(Action), controller, settingsHandler);
        panel.GetType().GetEvent("SettingsRequested").AddEventHandler(panel, handler);
        object button = Get(panel, "settingsButton");
        Assert.That(button, Is.Not.Null);
        object click = button.GetType().GetProperty("onClick").GetValue(button);
        click.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(click, null);
        bool opened = (bool)manager.GetType().GetMethod("IsOpen").MakeGenericMethod(RuntimeType("SettingsPanel")).Invoke(manager, null);
        Assert.That(opened, Is.True);
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public void StartButtonRequiresHostAndTwoToFourReadyPlayers(int count, bool expected)
    {
        GameObject roomOwner = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Panels/RoomPanel.prefab"));
        objects.Add(roomOwner);
        Component panel = roomOwner.GetComponent(RuntimeType("RoomPanel"));
        panel.GetType().GetMethod("Init").Invoke(panel, null);
        object room = Activator.CreateInstance(RuntimeType("RoomStateMessage"));
        room.GetType().GetField("roomCode").SetValue(room, "TEST");
        room.GetType().GetField("status").SetValue(room, "WAITING");
        var members = (System.Collections.IList)room.GetType().GetField("members").GetValue(room);
        for (int i = 0; i < count; i++)
        {
            object player = Activator.CreateInstance(RuntimeType("RoomPlayer"));
            player.GetType().GetField("playerId").SetValue(player, "p" + i);
            player.GetType().GetField("team").SetValue(player, i);
            player.GetType().GetField("ready").SetValue(player, true);
            members.Add(player);
        }
        panel.GetType().GetMethod("ShowRoom").Invoke(panel, new object[] { room, "p0" });
        Component button = (Component)Get(panel, "startGameButton");
        Assert.That(button.gameObject.activeSelf, Is.False);
        int sent = 0;
        panel.GetType().GetMethod("ConfigureGameStart").Invoke(panel, new object[] { true, (Action)(() => sent++) });
        Assert.That(button.gameObject.activeSelf, Is.EqualTo(expected));
        if (expected)
        {
            Call(panel, "OnStartGameClicked"); Call(panel, "OnStartGameClicked");
            Assert.That(sent, Is.EqualTo(1));
            panel.GetType().GetMethod("SetInteraction").Invoke(panel, new object[] { true });
            members[0].GetType().GetField("ready").SetValue(members[0], false);
            panel.GetType().GetMethod("ShowRoom").Invoke(panel, new object[] { room, "p0" });
            Assert.That(button.gameObject.activeSelf, Is.False);
        }
        panel.GetType().GetMethod("ConfigureGameStart").Invoke(panel, new object[] { false, (Action)(() => sent++) });
        Assert.That(button.gameObject.activeSelf, Is.False);
    }
}
