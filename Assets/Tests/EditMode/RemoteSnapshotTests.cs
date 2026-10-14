using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RemoteSnapshotTests
{
    private GameObject socketObject;
    private object session;
    private Type sessionType;
    private TestBoard board;
    private GameState state;
    private TurnManager turn;

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [SetUp]
    public void SetUp()
    {
        board = new TestBoard();
        state = new GameState(board.State);
        turn = new TurnManager(state, 3);
        var setup = new GameSetup { teamCount = 3, piecesPerTeam = 2, localTeam = 0 };
        foreach (var piece in setup.EnumeratePieces()) board.State.AddPiece(piece.id, piece.team);
        var events = new GameEvents();
        var pool = new AugmentPool(new SystemRandomProvider());
        pool.Register("Monk", team => new MonkAugment(team));
        var rules = new GameRules(state, turn, events,
            new YutThrower(new SystemRandomProvider()), new StandardGetResult(), pool);
        socketObject = new GameObject("SnapshotTestSocket");
        Component socket = socketObject.AddComponent(RuntimeType("WebSocketClient"));
        sessionType = RuntimeType("RemoteGameSession");
        session = Activator.CreateInstance(sessionType,
            new object[] { socket, rules, events, state, turn, setup, null });
    }

    [TearDown]
    public void TearDown()
    {
        (session as IDisposable)?.Dispose();
        UnityEngine.Object.DestroyImmediate(socketObject);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field).SetValue(target, value);

    private object Snapshot(string phase = "PLAYING")
    {
        object snapshot = Activator.CreateInstance(RuntimeType("GameSnapshotMessage"));
        Set(snapshot, "phase", phase);
        Set(snapshot, "currentTeam", 1);
        Set(snapshot, "myTeam", 0);
        Set(snapshot, "turnNumber", 4);
        Set(snapshot, "lastSeq", 40L);
        Set(snapshot, "remainingThrows", 0);
        Set(snapshot, "storedResults", new List<YutResult> { YutResult.Do, YutResult.Gae });
        Set(snapshot, "ownedAugments", new Dictionary<int, List<string>> { { 0, new List<string> { "Monk" } } });
        return snapshot;
    }

    private static void AddPiece(object snapshot, int id, int team, string pieceState, int node, int? previous = null)
    {
        object piece = Activator.CreateInstance(RuntimeType("PieceSnapshot"));
        Set(piece, "pieceId", id);
        Set(piece, "team", team);
        Set(piece, "state", pieceState);
        Set(piece, "nodeId", node);
        Set(piece, "cameFromNodeId", previous);
        ((IList)snapshot.GetType().GetField("pieces").GetValue(snapshot)).Add(piece);
    }

    private object PopulatedSnapshot()
    {
        object snapshot = Snapshot();
        AddPiece(snapshot, 0, 0, "ON_BOARD", 3, 2);
        AddPiece(snapshot, 1, 0, "FINISHED", 0);
        AddPiece(snapshot, 100, 1, "WAITING", 0);
        AddPiece(snapshot, 101, 1, "ON_BOARD", 5, 4);
        return snapshot;
    }

    private void Apply(object snapshot) => sessionType.GetMethod("ApplySnapshot",
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new[] { snapshot });

    [Test]
    public void Snapshot_RestoresPiecesAugmentTurnAndResults_RemovesAbsentTeam()
    {
        Apply(PopulatedSnapshot());
        Assert.That(board.State.GetPiece(0).Node.Id, Is.EqualTo(3));
        Assert.That(board.State.GetPiece(0).CameFrom.Id, Is.EqualTo(2));
        Assert.That(board.State.GetPiece(1).IsFinished, Is.True);
        Assert.That(board.State.GetPiece(100).IsWaiting, Is.True);
        Assert.That(board.State.GetPiece(200), Is.Null);
        Assert.That(board.State.GetAugments(0).Single().Id, Is.EqualTo("Monk"));
        Assert.That(turn.CurrentTeam, Is.EqualTo(1));
        Assert.That(state.RemainingThrows, Is.Zero);
        Assert.That(state.StoredResults, Is.EqualTo(new[] { YutResult.Do, YutResult.Gae }));
    }

    [Test]
    public void RepeatedSnapshot_DoesNotDuplicateAugment()
    {
        Apply(PopulatedSnapshot());
        Apply(PopulatedSnapshot());
        Assert.That(board.State.GetAugments(0).Count, Is.EqualTo(1));
    }

    [TestCase(4, 2)]
    [TestCase(5, 0)]
    public void TurnReplay_PreservesResultsOnlyForSameTurn(int turnNumber, int expectedResults)
    {
        Apply(PopulatedSnapshot());
        object message = Activator.CreateInstance(RuntimeType("TurnChangedMessage"));
        Set(message, "team", 1);
        Set(message, "turnNumber", turnNumber);
        Set(message, "remainingThrows", 1);
        sessionType.GetMethod("ApplyTurn", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session, new[] { message });
        Assert.That(state.StoredResults.Count, Is.EqualTo(expectedResults));
        Assert.That(state.RemainingThrows, Is.EqualTo(1));
    }

    [Test]
    public void InvalidNode_IsRejectedBeforeAnyPieceChanges()
    {
        object snapshot = Snapshot();
        AddPiece(snapshot, 0, 0, "ON_BOARD", 3, 2);
        AddPiece(snapshot, 1, 0, "ON_BOARD", 999);
        Assert.Throws<TargetInvocationException>(() => Apply(snapshot));
        Assert.That(board.State.GetPiece(0).IsWaiting, Is.True);
        Assert.That(board.State.Pieces.Count, Is.EqualTo(6));
    }

    [Test]
    public void EndedEmptyBoard_RemovesAllPiecesAndRestoresWinner()
    {
        object snapshot = Snapshot("ENDED");
        Set(snapshot, "winnerTeam", 1);
        Apply(snapshot);
        Assert.That(board.State.Pieces, Is.Empty);
        Assert.That(state.WinnerTeam, Is.EqualTo(1));
    }

    [Test]
    public void Snapshot_ReleasesPendingSyncAndRestoresSequence()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        sessionType.GetField("awaitingSnapshot", flags).SetValue(session, true);
        sessionType.GetField("syncRequested", flags).SetValue(session, true);
        Assert.That((bool)sessionType.GetProperty("IsCommandPending").GetValue(session), Is.True);
        Apply(PopulatedSnapshot());
        Assert.That((bool)sessionType.GetProperty("IsCommandPending").GetValue(session), Is.False);
        Assert.That((long)sessionType.GetField("lastSequence", flags).GetValue(session), Is.EqualTo(40));
    }
    [Test]
    public void AugmentChoices_PreservesServerTimeLimitBeforeNotifyingUi()
    {
        object message = Activator.CreateInstance(RuntimeType("AugmentChoicesMessage"));
        Set(message, "team", 0);
        Set(message, "augmentIds", new List<string> { "Monk" });
        Set(message, "timeLimitSec", 18);
        sessionType.GetMethod("ApplyAugmentChoices", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session, new[] { message });
        Assert.That(sessionType.GetProperty("AugmentSelectionTimeLimitSec", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session), Is.EqualTo(18));
    }

    private static object ErrorEnvelope(string code)
    {
        object error = Activator.CreateInstance(RuntimeType("ErrorMessage"));
        Set(error, "code", code); Set(error, "message", "복귀 거절");
        Type serializer = RuntimeType("MessageSerializer");
        MethodInfo serialize = serializer.GetMethods().First(method => method.Name == "Serialize" && method.IsGenericMethod);
        string json = (string)serialize.MakeGenericMethod(error.GetType()).Invoke(null,
            new[] { Enum.Parse(RuntimeType("MessageType"), "ERROR"), error, (object)0L });
        return serializer.GetMethod("Deserialize").Invoke(null, new object[] { json });
    }

    [TestCase("REMOVED_FROM_GAME")]
    [TestCase("GAME_ALREADY_ENDED")]
    [TestCase("GAME_NOT_FOUND")]
    [TestCase("PLAYER_NOT_IN_GAME")]
    public void TerminalReconnectErrorStopsSessionAndNotifiesOnce(string code)
    {
        int rejected = 0;
        sessionType.GetEvent("ReconnectRejected").AddEventHandler(session, new Action<string>(_ => rejected++));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        sessionType.GetField("commandPending", flags).SetValue(session, true);
        sessionType.GetField("awaitingSnapshot", flags).SetValue(session, true);
        sessionType.GetField("syncRequested", flags).SetValue(session, true);
        sessionType.GetMethod("ReplayPendingMessages").Invoke(session, null);
        MethodInfo receive = sessionType.GetMethod("OnMessageReceived", flags);
        receive.Invoke(session, new[] { ErrorEnvelope(code) });
        receive.Invoke(session, new[] { ErrorEnvelope(code) });
        Assert.That(rejected, Is.EqualTo(1));
        Assert.That(sessionType.GetField("reconnectRejected", flags).GetValue(session), Is.True);
        Assert.That(sessionType.GetField("awaitingSnapshot", flags).GetValue(session), Is.False);
        Assert.That(sessionType.GetField("syncRequested", flags).GetValue(session), Is.False);
        Assert.That(sessionType.GetProperty("IsCommandPending").GetValue(session), Is.True);
    }

    [Test]
    public void OrdinaryErrorDoesNotForceLobbyReturn()
    {
        int fatal = 0, ordinary = 0;
        sessionType.GetEvent("ReconnectRejected").AddEventHandler(session, new Action<string>(_ => fatal++));
        sessionType.GetEvent("RequestRejected").AddEventHandler(session, new Action<string>(_ => ordinary++));
        sessionType.GetMethod("ReplayPendingMessages").Invoke(session, null);
        sessionType.GetMethod("OnMessageReceived", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session, new[] { ErrorEnvelope("NOT_YOUR_TURN") });
        Assert.That(fatal, Is.Zero);
        Assert.That(ordinary, Is.EqualTo(1));
        Assert.That(sessionType.GetProperty("IsCommandPending").GetValue(session), Is.False);
    }

}
