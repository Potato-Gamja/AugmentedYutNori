using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class GamePointerInputTests : InputTestFixture
{
    private Touchscreen touchscreen;
    private Mouse mouse;
    private GameObject canvasObject;
    private GameObject eventObject;
    private GameObject cameraObject;
    private RenderTexture renderTexture;
    private EventSystem previousEventSystem;
    private static Type InputType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("GamePointerInput")).First(t => t != null);

    public override void Setup()
    {
        base.Setup();
        previousEventSystem = EventSystem.current;
        touchscreen = InputSystem.AddDevice<Touchscreen>();
        mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.Update();
    }

    public override void TearDown()
    {
        if (canvasObject != null)
        {
            typeof(BaseRaycaster).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvasObject.GetComponent<GraphicRaycaster>(), null);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
        if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
        if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
        if (eventObject != null)
        {
            typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(eventObject.GetComponent<EventSystem>(), null);
            UnityEngine.Object.DestroyImmediate(eventObject);
        }
        if (previousEventSystem != null) EventSystem.current = previousEventSystem;
        base.TearDown();
    }

    private static bool Read(out Vector2 position)
    {
        object[] args = { Vector2.zero };
        bool pressed = (bool)InputType.GetMethod("TryGetPress").Invoke(null, args);
        position = (Vector2)args[0];
        return pressed;
    }

    private void Touch(TouchPhase phase, Vector2 position, int id = 1)
    {
        InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position });
        InputSystem.Update();
    }

    [Test]
    public void TouchBeganUsesTouchPositionEvenWhenMouseIsElsewhere()
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(900, 500) }.WithButton(MouseButton.Left));
        Touch(TouchPhase.Began, new Vector2(220, 180));
        Assert.That(Read(out var position), Is.True);
        Assert.That(position, Is.EqualTo(new Vector2(220, 180)));
    }

    [Test]
    public void HeldTouchAndReleaseDoNotBecomeAnotherMouseClick()
    {
        Touch(TouchPhase.Began, new Vector2(220, 180));
        Assert.That(Read(out _), Is.True);
        InputSystem.Update();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        Touch(TouchPhase.Moved, new Vector2(230, 180));
        Assert.That(Read(out _), Is.False);
        Touch(TouchPhase.Ended, new Vector2(230, 180));
        Assert.That(Read(out _), Is.False);
    }

    [Test]
    public void SecondFingerDoesNotSelectAgain()
    {
        Touch(TouchPhase.Began, new Vector2(220, 180));
        InputSystem.Update();
        Touch(TouchPhase.Began, new Vector2(300, 180), 2);
        Assert.That(Read(out _), Is.False);
    }

    [Test]
    public void MouseClickStillWorksWithoutActiveTouch()
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(360, 240) }.WithButton(MouseButton.Left));
        InputSystem.Update();
        Assert.That(Read(out var position), Is.True);
        Assert.That(position, Is.EqualTo(new Vector2(360, 240)));
    }

    [Test]
    public void UiBlockingUsesCurrentTouchPositionInsteadOfCachedMousePosition()
    {
        previousEventSystem = EventSystem.current;
        eventObject = new GameObject("Pointer test events", typeof(EventSystem));
        var eventSystem = eventObject.GetComponent<EventSystem>();
        // EventSystem is not ExecuteAlways; enable its registration explicitly in EditMode.
        typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(eventSystem, null);
        EventSystem.current = eventSystem;
        canvasObject = new GameObject("Pointer test UI", typeof(Canvas), typeof(GraphicRaycaster));
        cameraObject = new GameObject("Pointer test camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        renderTexture = new RenderTexture(256, 256, 16);
        camera.targetTexture = renderTexture;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        typeof(BaseRaycaster).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(canvasObject.GetComponent<GraphicRaycaster>(), null);
        var imageObject = new GameObject("Blocking UI", typeof(RectTransform), typeof(Image));
        var rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(50, 50);
        rect.sizeDelta = new Vector2(100, 100);
        Canvas.ForceUpdateCanvases();
        // Build real UI geometry in EditMode so GraphicRaycaster can test its depth.
        camera.Render();
        var method = InputType.GetMethod("IsOverUI");
        Assert.That(method.Invoke(null, new object[] { new Vector2(80, 80) }), Is.True);
        Assert.That(method.Invoke(null, new object[] { new Vector2(160, 80) }), Is.False);
    }
}
