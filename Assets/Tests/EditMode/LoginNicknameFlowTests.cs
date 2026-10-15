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
}
