using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NodeClickBoundsTests
{
    private GameObject nodeObject;
    private Texture2D texture;
    private Sprite sprite;
    private Component node;
    private SpriteRenderer renderer;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [SetUp]
    public void SetUp()
    {
        nodeObject = new GameObject("NodeClickTest", typeof(SpriteRenderer));
        node = nodeObject.AddComponent(RuntimeType("NodeView"));
        texture = new Texture2D(20, 40);
        sprite = Sprite.Create(texture, new Rect(0, 0, 20, 40), new Vector2(.5f, .5f), 10);
        renderer = nodeObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        nodeObject.transform.position = new Vector3(5, 3, 0);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(nodeObject);
        UnityEngine.Object.DestroyImmediate(sprite);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    [TestCase(BoardNodeType.Start, .9f, 1.9f, true)]
    [TestCase(BoardNodeType.End, .9f, 1.9f, true)]
    [TestCase(BoardNodeType.Start, 1.1f, 0f, false)]
    [TestCase(BoardNodeType.End, 0f, 2.1f, false)]
    [TestCase(BoardNodeType.Normal, .9f, .9f, false)]
    [TestCase(BoardNodeType.Normal, .5f, .5f, true)]
    public void UsesRectangleOnlyForStartAndEnd(BoardNodeType type, float x, float y, bool expected)
    {
        node.GetType().GetField("type", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(node, type);
        MethodInfo contains = RuntimeType("GameFlowSelectionController").GetMethod("ContainsNode",
            BindingFlags.Static | BindingFlags.NonPublic);
        bool actual = (bool)contains.Invoke(null,
            new object[] { node, renderer, nodeObject.transform.position + new Vector3(x, y, 0) });
        Assert.That(actual, Is.EqualTo(expected));
    }
}
