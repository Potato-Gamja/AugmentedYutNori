using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyTutorialTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Scene preview;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Field(Component owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Call(object owner, string method) => owner.GetType().GetMethod(method).Invoke(owner, null);
    private Component Find(string name) => preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(Runtime(name), true)).First();
    private static Component Open(Component manager, string name) => (Component)manager.GetType().GetMethod("Open").MakeGenericMethod(Runtime(name)).Invoke(manager, null);

    [TearDown]
    public void TearDown()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
    }

    [Test]
    public void PagesFitAndNavigationClosesAndResets()
    {
        root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UIv2/Popups/TutorialPanel.prefab"));
        var panel = root.GetComponent(Runtime("TutorialPanel")); Call(panel, "Init"); Call(panel, "Open");
        var previous = (Button)Field(panel, "previousButton"); var next = (Button)Field(panel, "nextButton");
        Assert.That(previous.interactable, Is.False);
        for (int page = 0; page < 5; page++)
        {
            Assert.That(panel.GetType().GetProperty("PageIndex").GetValue(panel), Is.EqualTo(page));
            var image = (Image)Field(panel, "pageImage");
            Assert.That(image.enabled && image.sprite != null, Is.True, "Page screenshot missing");
            string[] expected = { "Room", "Augments_Select", "MainGame_YutThrow", "MainGame_Move", "MainGame_Move" };
            Assert.That(image.sprite.texture.name, Is.EqualTo(expected[page]));
            Assert.That(image.preserveAspect, Is.True);
            var body = (Component)Field(panel, "bodyText");
            float height = (float)body.GetType().GetProperty("preferredHeight").GetValue(body);
            Assert.That(height, Is.LessThanOrEqualTo(body.GetComponent<RectTransform>().rect.height), "Page " + page + " overflows");
            next.onClick.Invoke();
        }
        Assert.That(root.activeSelf, Is.False);
        Call(panel, "Open");
        Assert.That(panel.GetType().GetProperty("PageIndex").GetValue(panel), Is.EqualTo(0));
        next.onClick.Invoke(); previous.onClick.Invoke(); previous.onClick.Invoke();
        Assert.That(panel.GetType().GetProperty("PageIndex").GetValue(panel), Is.EqualTo(0));
        ((Button)Field(panel, "closeButton")).onClick.Invoke(); Assert.That(root.activeSelf, Is.False);
        Assert.That(root.transform.Find("Dim").GetComponent<Image>().raycastTarget, Is.True);
    }

    [TestCase("Lobby", 1)]
    [TestCase("Game", 0)]
    [TestCase("Login", 0)]
    public void TutorialIsRegisteredOnlyInLobby(string scene, int expected)
    {
        preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + scene + ".unity");
        var manager = Find("UIManager");
        var list = (System.Collections.IEnumerable)Field(manager, "prefabs");
        Assert.That(list.Cast<object>().Count(p => p != null && p.GetType().Name == "TutorialPanel"), Is.EqualTo(expected));
    }

    [Test]
    public void MenuButtonUsesLobbyRouteAndReusesOnePopup()
    {
        preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Lobby.unity");
        var manager = Find("UIManager"); manager.GetType().GetMethod("Awake", Private).Invoke(manager, null);
        var main = Open(manager, "MainMenuPanel"); var controller = Find("LobbyController");
        var method = controller.GetType().GetMethod("OnTutorialRequested", Private);
        var handler = (Action)Delegate.CreateDelegate(typeof(Action), controller, method);
        main.GetType().GetEvent("TutorialRequested").AddEventHandler(main, handler);
        var button = (Button)Field(main, "tutorialButton"); Assert.That(button, Is.Not.Null);
        button.onClick.Invoke(); button.onClick.Invoke();
        var popups = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(Runtime("TutorialPanel"), true)).ToArray();
        Assert.That(popups.Length, Is.EqualTo(1)); Assert.That(popups[0].gameObject.activeInHierarchy, Is.True);
    }
}
