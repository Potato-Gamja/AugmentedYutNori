using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class VeteranFlowLifecycleTests
{
    private GameObject root;
    private object flow;
    private Type type;
    private BoardState board;

    [SetUp]
    public void SetUp()
    {
        type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GameFlowController")).First(t => t != null);
        root = new GameObject("VeteranFlowTest");
        root.SetActive(false);
        flow = root.AddComponent(type);
        board = new TestBoard().State;
        Set("boardState", board);
        Set("setup", new GameSetup { teamCount = 3, piecesPerTeam = 4 });
        Set("teamCount", 3);
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

    private void Set(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, value);
    private object Call(string name, params object[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, args);
    private HashSet<int> Removed => (HashSet<int>)type.GetField("removedTeams", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);

    [Test]
    public void RemovedTeam_DoesNotBlockAllAugmentsConfirmed()
    {
        board.AddAugment(new VeteranAugment(0));
        board.AddAugment(new MonkAugment(1));
        Assert.That(Call("AllTeamsHaveAugment"), Is.False);
        Removed.Add(2);
        Assert.That(Call("AllTeamsHaveAugment"), Is.True);
        Assert.That(Call("PieceCount", 0), Is.EqualTo(3));
        Assert.That(Call("PieceCount", 1), Is.EqualTo(4));
        Assert.That(Call("PieceCount", 2), Is.Zero);
    }

    [Test]
    public void AlreadySelectedButRemovedTeam_GetsNoPieces()
    {
        board.AddAugment(new VeteranAugment(1));
        Removed.Add(1);
        Assert.That(Call("PieceCount", 1), Is.Zero);
    }

    [Test]
    public void SelectionSnapshot_DoesNotCreatePiecesOrMarkEveryTeamRemoved()
    {
        Type snapshotType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GameSnapshotMessage")).First(t => t != null);
        object snapshot = Activator.CreateInstance(snapshotType);
        snapshotType.GetField("phase").SetValue(snapshot, "AUGMENT_SELECT");
        Call("OnAugmentsRestored", snapshot);
        Assert.That(board.Pieces.Count, Is.Zero);
        Assert.That(Removed, Is.Empty);
        Assert.That(type.GetField("piecesCreated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow), Is.False);
    }
}
