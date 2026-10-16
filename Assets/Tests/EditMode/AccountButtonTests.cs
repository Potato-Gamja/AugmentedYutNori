using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AccountButtonTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private string prefix;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static Component Panel(GameObject owner, string type) => owner.GetComponent(Runtime(type));
    private static object Field(Component owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Call(object owner, string name, params object[] values) => owner.GetType().GetMethod(name).Invoke(owner, values);

    [TearDown]
    public void TearDown()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (prefix == null) return;
        foreach (string name in new[] { "playerId", "token", "nickname", "authType", "refreshToken", "gold" })
            PlayerPrefs.DeleteKey(prefix + "player." + name);
        PlayerPrefs.DeleteKey(prefix + "ReconnectGame");
    }

    [Test]
    public void GuestButtonIsBoundAndBlockedWhileBusy()
    {
        root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Panels/LoginPanel.prefab"));
        var panel = Panel(root, "LoginPanel"); Call(panel, "Init");
        int requests = 0;
        panel.GetType().GetEvent("GuestLoginRequested").AddEventHandler(panel, (Action)(() => requests++));
        var guest = (Button)Field(panel, "guestLoginButton");
        Assert.That(guest, Is.Not.Null);
        guest.onClick.Invoke(); Assert.That(requests, Is.EqualTo(1));
        Call(panel, "SetBusy", true); Assert.That(guest.interactable, Is.False);
        guest.onClick.Invoke(); Assert.That(requests, Is.EqualTo(1));
        Call(panel, "SetBusy", false); Assert.That(guest.interactable, Is.True);
    }

    [Test]
    public void NewGuestClearsPreviousAccountDataOnlyAfterValidResponse()
    {
        prefix = "AccountButtonTests." + Guid.NewGuid() + ".";
        PlayerPrefs.SetString(prefix + "player.playerId", "old-player");
        PlayerPrefs.SetString(prefix + "player.token", "old-token");
        PlayerPrefs.SetString(prefix + "player.authType", "google");
        PlayerPrefs.SetString(prefix + "player.refreshToken", "old-refresh");
        PlayerPrefs.SetInt(prefix + "player.gold", 100);
        PlayerPrefs.SetString(prefix + "ReconnectGame", "old-game");
        var type = Runtime("PlayerIdentity");
        var identity = Activator.CreateInstance(type, prefix);
        Assert.Throws<TargetInvocationException>(() => Call(identity, "ApplyGuest", new object[] { null }));
        Assert.That(type.GetProperty("PlayerId").GetValue(identity), Is.EqualTo("old-player"));
        var response = Activator.CreateInstance(Runtime("GuestLoginResponse"));
        foreach (var pair in new[] { ("playerId", "guest-player"), ("guestToken", "guest-token"), ("nickname", "게스트") })
            response.GetType().GetField(pair.Item1).SetValue(response, pair.Item2);
        Call(identity, "ApplyGuest", response);
        var restored = Activator.CreateInstance(type, prefix);
        Assert.That(type.GetProperty("PlayerId").GetValue(restored), Is.EqualTo("guest-player"));
        Assert.That(type.GetProperty("Token").GetValue(restored), Is.EqualTo("guest-token"));
        Assert.That(type.GetProperty("IsGoogleLogin").GetValue(restored), Is.EqualTo(false));
        Assert.That(type.GetProperty("Gold").GetValue(restored), Is.EqualTo(0));
        Assert.That(type.GetProperty("FirebaseRefreshToken").GetValue(restored), Is.EqualTo(""));
        Assert.That(PlayerPrefs.HasKey(prefix + "ReconnectGame"), Is.False);
    }

    [Test]
    public void WithdrawButtonShowsPendingNoticeWithoutLoggingOut()
    {
        root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Popups/ProfilePanel.prefab"));
        var panel = Panel(root, "ProfilePanel"); Call(panel, "Init");
        int logouts = 0;
        panel.GetType().GetEvent("LogoutRequested").AddEventHandler(panel, (Action)(() => logouts++));
        var button = (Button)Field(panel, "withdrawButton"); Assert.That(button, Is.Not.Null);
        button.onClick.Invoke();
        var status = (Component)Field(panel, "statusText");
        var text = (string)status.GetType().GetProperty("text").GetValue(status);
        Assert.That(text, Does.Contain("서버 연결 준비 중")); Assert.That(logouts, Is.Zero);
    }
}
