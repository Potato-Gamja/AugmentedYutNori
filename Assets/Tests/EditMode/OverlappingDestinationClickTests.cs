using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OverlappingDestinationClickTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new();
    private Texture2D texture;
    private Sprite sprite;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private Component Node(int id, BoardNodeType type, Vector3 position)
    {
        var go = new GameObject("Overlap node " + id, typeof(SpriteRenderer));
        objects.Add(go);
        go.transform.position = position;
        go.GetComponent<SpriteRenderer>().sprite = sprite;
        var node = go.AddComponent(Runtime("NodeView"));
        node.GetType().GetField("id", Private).SetValue(node, id);
        node.GetType().GetField("type", Private).SetValue(node, type);
        return node;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear();
        if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
    }

    [TestCase(false, 0f)]
    [TestCase(true, 0f)]
    [TestCase(false, 0.1f)]
    [TestCase(true, 0.1f)]
    public void OverlappingStartCannotBlockExactEndOrFinishedMove(bool finished, float endOffset)
    {
        texture = new Texture2D(20, 20);
        sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), new Vector2(.5f, .5f), 20);
        var definition = new BoardDefinition { startNodeId = 0, nodes = new List<NodeDefinition>
        {
            new NodeDefinition { id = 0, type = BoardNodeType.Start, next = new List<int> { 1 }, prev = new List<int> { 20 } },
            new NodeDefinition { id = 1, type = BoardNodeType.Normal, next = new List<int> { 20 }, prev = new List<int> { 20 } },
            new NodeDefinition { id = 20, type = BoardNodeType.End, next = new List<int> { 0 }, prev = new List<int> { 1 } }
        } };
        var graph = definition.CreateGraph();
        var root = new GameObject("Overlap board"); objects.Add(root); root.SetActive(false);
        var board = root.AddComponent(Runtime("BoardView"));
        board.GetType().GetProperty("Graph").SetValue(board, graph);
        var start = Node(0, BoardNodeType.Start, Vector3.zero);
        var end = Node(20, BoardNodeType.End, new Vector3(endOffset, 0, 0));
        var middle = Node(1, BoardNodeType.Normal, new Vector3(5, 0, 0));
        var views = (IDictionary)board.GetType().GetField("views", Private).GetValue(board);
        views.Add(graph.GetNode(0), start);
        views.Add(graph.GetNode(1), middle);
        views.Add(graph.GetNode(20), end);
        MoveOption? applied = null;
        int appliedCount = 0;
        var pieces = Array.CreateInstance(Runtime("PieceView"), 0);
        object controller = Activator.CreateInstance(Runtime("GameFlowSelectionController"), new object[]
        { board, new BoardState(graph), null, null, null, pieces, new Func<int>(() => 0), new Action<MoveOption>(o => { applied = o; appliedCount++; }), null });
        var selected = root.AddComponent(Runtime("PieceView"));
        controller.GetType().GetField("<SelectedPiece>k__BackingField", Private).SetValue(controller, selected);
        var option = new MoveOption(resultIndex: 0, result: YutResult.Do, destinationNodeId: finished ? 0 : 20, modifiedMoveCount: 1, isFinished: finished);
        var targets = (IDictionary)controller.GetType().GetField("targets", Private).GetValue(controller);
        targets.Add(end, new List<MoveOption> { option });

        // The hidden Start is enumerated first and is at least as close as the valid End.
        controller.GetType().GetMethod("HandleClick").Invoke(controller, new object[] { Vector3.zero });
        Assert.That(appliedCount, Is.EqualTo(1));
        Assert.That(applied.HasValue, Is.True);
        Assert.That(applied.Value, Is.EqualTo(option));
        Assert.That(applied.Value.DestinationNodeId, Is.EqualTo(finished ? 0 : 20));
        Assert.That(applied.Value.IsFinished, Is.EqualTo(finished));
    }
}
