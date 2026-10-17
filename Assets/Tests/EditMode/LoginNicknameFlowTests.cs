using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginNicknameFlowTests
{
    private Scene preview;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private Component Find(string name) => preview.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren(Runtime(name), true)).First();
    private static object Open(Component manager, string type) => manager.GetType().GetMethod("Open")
        .MakeGenericMethod(Runtime(type)).Invoke(manager, null);

    [TearDown]
    public void TearDown()
    {
        if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
    }

    [Test]
    public void FirstLoginOpensVisibleNicknameInputUsingActualLoginSceneReferences()
    {
        preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Login.unity");
        var manager = Find("UIManager");
        manager.GetType().GetMethod("Awake", Private).Invoke(manager, null);
        var login = (Component)Open(manager, "LoginPanel");
        var controller = Find("LoginController");
        controller.GetType().GetMethod("OpenNicknamePanel", Private).Invoke(controller, null);
        var nickname = (Component)controller.GetType().GetField("nicknamePanel", Private).GetValue(controller);
        Assert.That(nickname, Is.Not.Null);
        Assert.That(nickname.gameObject.activeInHierarchy, Is.True);
        Assert.That(login.gameObject.activeSelf, Is.False);
        var input = (Component)nickname.GetType().GetField("nicknameInput", Private).GetValue(nickname);
        var confirm = (Component)nickname.GetType().GetField("confirmButton", Private).GetValue(nickname);
        Assert.That(input.gameObject.activeInHierarchy, Is.True);
        Assert.That(confirm.gameObject.activeInHierarchy, Is.True);
        Assert.That(nickname.GetComponentInParent<Canvas>().enabled, Is.True);
        Debug.Log("[Login UI verification] Nickname panel, input and confirm active; Login panel closed.");
    }
    [TestCase(true)]
    [TestCase(false)]
    public void BrowserPlaceholderStaysHiddenAfterTypingAndClearing(bool initiallyActive)
    {
        preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Login.unity");
        var manager = Find("UIManager");
        manager.GetType().GetMethod("Awake", Private).Invoke(manager, null);
        var nickname = (Component)Open(manager, "NicknamePanel");
        var input = (Component)nickname.GetType().GetField("nicknameInput", Private).GetValue(nickname);
        var placeholder = (Component)input.GetType().GetProperty("placeholder").GetValue(input);
        placeholder.gameObject.SetActive(initiallyActive);
        var hide = nickname.GetType().GetMethod("SetBrowserPlaceholderHidden", Private);
        hide.Invoke(nickname, new object[] { true });
        for (int i = 0; i < 3; i++)
        {
            input.GetType().GetProperty("text").SetValue(input, "감자");
            input.GetType().GetProperty("text").SetValue(input, "");
            input.GetType().GetMethod("ForceLabelUpdate").Invoke(input, null);
            Assert.That(placeholder.gameObject.activeInHierarchy, Is.False,
                "Clearing text must not reveal Unity's placeholder under the browser input.");
        }
        hide.Invoke(nickname, new object[] { false });
        Assert.That(placeholder.gameObject.activeSelf, Is.EqualTo(initiallyActive));
    }

    [TestCase("anonymous", "saved-refresh", true)]
    [TestCase("google", "saved-refresh", true)]
    [TestCase("anonymous", "", false)]
    [TestCase("guest", "saved-refresh", false)]
    public void SavedLoginRequiresFirebaseAccountAndRefreshToken(string authType, string refreshToken, bool expected)
    {
        string prefix = "GuestFlowTest." + Guid.NewGuid() + ".";
        try
        {
            PlayerPrefs.SetString(prefix + "player.playerId", "saved-account");
            PlayerPrefs.SetString(prefix + "player.token", "saved-token");
            PlayerPrefs.SetString(prefix + "player.authType", authType);
            PlayerPrefs.SetString(prefix + "player.refreshToken", refreshToken);
            var identity = Activator.CreateInstance(Runtime("PlayerIdentity"), prefix);
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Login.unity");
            var manager = Find("UIManager");
            manager.GetType().GetMethod("Awake", Private).Invoke(manager, null);
            Open(manager, "LoginPanel");
            var controller = Find("LoginController");
            controller.GetType().GetField("playerIdentity", Private).SetValue(controller, identity);
            Assert.That(controller.GetType().GetMethod("HasSavedLogin", Private).Invoke(controller, null), Is.EqualTo(expected));
            // Duplicate guest clicks during authentication must not start another request or open nickname entry early.
            controller.GetType().GetField("signingIn", Private).SetValue(controller, true);
            controller.GetType().GetMethod("OnGuestLoginRequested", Private).Invoke(controller, null);
            Assert.That(controller.GetType().GetField("nicknamePanel", Private).GetValue(controller), Is.Null);
            Assert.That(identity.GetType().GetProperty("Token").GetValue(identity), Is.EqualTo("saved-token"));
        }
        finally
        {
            foreach (string name in new[] { "playerId", "token", "authType", "refreshToken" })
                PlayerPrefs.DeleteKey(prefix + "player." + name);
        }
    }

}
