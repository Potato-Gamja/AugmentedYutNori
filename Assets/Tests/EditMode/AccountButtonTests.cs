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
    public void FirebaseGuestLoginPersistsCredentialsAndPreservesAuthTypeAfterNicknameChange()
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
        var response = Activator.CreateInstance(Runtime("FirebaseLoginResponse"));
        foreach (var pair in new[] { ("playerId", "guest-player"), ("token", "guest-jwt"), ("nickname", "") })
            response.GetType().GetField(pair.Item1).SetValue(response, pair.Item2);
        response.GetType().GetField("guest").SetValue(response, true);
        Call(identity, "ApplyLogin", response, "anonymous-refresh");
        Call(identity, "ApplyNickname", "게스트");
        var restored = Activator.CreateInstance(type, prefix);
        Assert.That(type.GetProperty("PlayerId").GetValue(restored), Is.EqualTo("guest-player"));
        Assert.That(type.GetProperty("Token").GetValue(restored), Is.EqualTo("guest-jwt"));
        Assert.That(type.GetProperty("IsGoogleLogin").GetValue(restored), Is.EqualTo(false));
        Assert.That(type.GetProperty("IsGuest").GetValue(restored), Is.EqualTo(true));
        Assert.That(type.GetProperty("HasFirebaseLogin").GetValue(restored), Is.EqualTo(true));
        Assert.That(type.GetProperty("Nickname").GetValue(restored), Is.EqualTo("게스트"));
        Assert.That(type.GetProperty("Gold").GetValue(restored), Is.EqualTo(0));
        Assert.That(type.GetProperty("FirebaseRefreshToken").GetValue(restored), Is.EqualTo("anonymous-refresh"));
        Call(identity, "ApplyGold", 250);
        Call(identity, "ApplyLogin", response, "rotated-refresh");
        restored = Activator.CreateInstance(type, prefix);
        Assert.That(type.GetProperty("PlayerId").GetValue(restored), Is.EqualTo("guest-player"));
        Assert.That(type.GetProperty("Gold").GetValue(restored), Is.EqualTo(250));
        Assert.That(type.GetProperty("FirebaseRefreshToken").GetValue(restored), Is.EqualTo("rotated-refresh"));
    }

    [Test]
    public void WithdrawRequiresTwoClicksAndIsBlockedWhileBusy()
    {
        root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Popups/ProfilePanel.prefab"));
        var panel = Panel(root, "ProfilePanel"); Call(panel, "Init");
        int logouts = 0;
        int withdrawals = 0;
        panel.GetType().GetEvent("LogoutRequested").AddEventHandler(panel, (Action)(() => logouts++));
        panel.GetType().GetEvent("WithdrawRequested").AddEventHandler(panel, (Action)(() => withdrawals++));
        var button = (Button)Field(panel, "withdrawButton"); Assert.That(button, Is.Not.Null);
        button.onClick.Invoke();
        var status = (Component)Field(panel, "statusText");
        var text = (string)status.GetType().GetProperty("text").GetValue(status);
        Assert.That(text, Does.Contain("삭제"));
        Assert.That(withdrawals, Is.Zero);
        Call(panel, "SetWithdrawBusy", true);
        Assert.That(button.interactable, Is.False);
        button.onClick.Invoke();
        Assert.That(withdrawals, Is.Zero);
        Call(panel, "SetWithdrawBusy", false);
        button.onClick.Invoke();
        Assert.That(withdrawals, Is.EqualTo(1));
        button.onClick.Invoke();
        Assert.That(withdrawals, Is.EqualTo(1), "A new request must require confirmation again.");
        Assert.That(logouts, Is.Zero);
    }

    [Test]
    public void ExpiredWithdrawalConfirmationRequiresTwoNewClicks()
    {
        root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Popups/ProfilePanel.prefab"));
        var panel = Panel(root, "ProfilePanel"); Call(panel, "Init");
        int requests = 0;
        panel.GetType().GetEvent("WithdrawRequested").AddEventHandler(panel, (Action)(() => requests++));
        var button = (Button)Field(panel, "withdrawButton");
        button.onClick.Invoke();
        panel.GetType().GetField("withdrawArmedUntil", Private).SetValue(panel, Time.unscaledTime - 1f);
        button.onClick.Invoke();
        Assert.That(requests, Is.Zero);
        button.onClick.Invoke();
        Assert.That(requests, Is.EqualTo(1));
    }

    [TestCase(-120, true)]
    [TestCase(1800, true)]
    [TestCase(7200, false)]
    public void JwtRefreshWindowIncludesExpiredAndSoonExpiringTokens(int secondsRemaining, bool expected)
    {
        long expiry = DateTimeOffset.UtcNow.AddSeconds(secondsRemaining).ToUnixTimeSeconds();
        string payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"exp\":" + expiry + "}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        bool actual = (bool)Runtime("JwtToken").GetMethod("IsExpiringWithin")
            .Invoke(null, new object[] { "header." + payload + ".signature", TimeSpan.FromHours(1) });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("legacy-token")]
    [TestCase("header.invalid.signature")]
    public void InvalidJwtDoesNotTriggerProactiveRefresh(string token)
    {
        Assert.That(Runtime("JwtToken").GetMethod("IsExpiringWithin")
            .Invoke(null, new object[] { token, TimeSpan.FromHours(1) }), Is.EqualTo(false));
    }
}
