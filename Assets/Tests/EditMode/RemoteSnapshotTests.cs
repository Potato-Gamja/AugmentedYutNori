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
        pool.Register("Veteran", team => new VeteranAugment(team));
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

    private static Action<T> SnapshotHandler<T>(Action<object> handler) => snapshot => handler(snapshot);

    [Test]
    public void VeteranSnapshot_RestoresAugmentsBeforeCreatingPieces_ThenRestoresPositions()
    {
        foreach (int team in Enumerable.Range(0, 3)) board.State.RemoveTeam(team);
        object snapshot = Snapshot();
        Set(snapshot, "ownedAugments", new Dictionary<int, List<string>>
        {
            { 0, new List<string> { "Veteran" } }, { 1, new List<string> { "Monk" } }
        });
        AddPiece(snapshot, 0, 0, "ON_BOARD", 3, 2);
        AddPiece(snapshot, 100, 1, "WAITING", 0);
        AddPiece(snapshot, 101, 1, "WAITING", 0);
        int callbackCount = 0;
        Action<object> handler = received =>
        {
            Assert.That(received, Is.SameAs(snapshot));
            Assert.That(board.State.GetAugments(0).Single(), Is.TypeOf<VeteranAugment>());
            if (callbackCount++ != 0) return;
            var setup = new GameSetup { teamCount = 3, piecesPerTeam = 2 };
            foreach (var piece in setup.EnumeratePieces(team => team == 2 ? 0 :
                         VeteranAugment.PieceCountFor(board.State, team, setup.piecesPerTeam)))
                board.State.AddPiece(piece.id, piece.team);
        };
        Delegate callback = (Delegate)typeof(RemoteSnapshotTests).GetMethod("SnapshotHandler",
            BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(snapshot.GetType())
            .Invoke(null, new object[] { handler });
        sessionType.GetEvent("AugmentsRestored").AddEventHandler(session, callback);
        Apply(snapshot);
        Apply(snapshot);
        Assert.That(callbackCount, Is.EqualTo(2));
        Assert.That(board.State.GetPiece(0).Node.Id, Is.EqualTo(3));
        Assert.That(board.State.GetPiece(1), Is.Null);
        Assert.That(board.State.Pieces.Count(), Is.EqualTo(3));
    }

    private void Receive(object payload, string type, long seq = 0)
    {
        Type serializer = RuntimeType("MessageSerializer");
        string json = (string)serializer.GetMethods().Single(method => method.Name == "Serialize" && method.IsGenericMethod)
            .MakeGenericMethod(payload.GetType()).Invoke(null,
                new[] { Enum.Parse(RuntimeType("MessageType"), type), payload, (object)seq });
        object envelope = serializer.GetMethod("Deserialize").Invoke(null, new object[] { json });
        sessionType.GetMethod("OnMessageReceived", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new[] { envelope });
    }

    private void ObserveSnapshots(Action<object> handler)
    {
        Delegate callback = (Delegate)typeof(RemoteSnapshotTests).GetMethod("SnapshotHandler",
            BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(RuntimeType("GameSnapshotMessage"))
            .Invoke(null, new object[] { handler });
        sessionType.GetEvent("SnapshotApplied").AddEventHandler(session, callback);
        sessionType.GetMethod("ReplayPendingMessages").Invoke(session, null);
    }

    [TestCase(40L)]
    [TestCase(41L)]
    public void LateSnapshotAfterMove_DoesNotReplayRestoreOrCancelMovement(long snapshotSequence)
    {
        int snapshots = 0, moves = 0;
        ObserveSnapshots(_ => snapshots++);
        sessionType.GetEvent("Moved").AddEventHandler(session, new Action<MoveResult>(_ => moves++));
        Receive(PopulatedSnapshot(), "GAME_SNAPSHOT");
        object move = Activator.CreateInstance(RuntimeType("MoveAppliedMessage"));
        Set(move, "team", 1); Set(move, "pieceId", 100); Set(move, "moveCount", 1);
        Set(move, "destinationNodeId", 1);
        Set(move, "movedPieceIds", new List<int> { 100 });
        Set(move, "capturedPieceIds", new List<int>());
        Receive(move, "MOVE_APPLIED", 41);
        Assert.That(moves, Is.EqualTo(1));
        object late = PopulatedSnapshot(); Set(late, "lastSeq", snapshotSequence);
        Receive(late, "GAME_SNAPSHOT");
        Assert.That(snapshots, Is.EqualTo(1), "중복 복원 이벤트가 이동 연출을 취소하면 안 된다.");
        Assert.That(board.State.GetPiece(100).Node.Id, Is.EqualTo(1));
        Assert.That(sessionType.GetField("lastSequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session), Is.EqualTo(41L));
    }

    [TestCase(YutResult.BackDo, 2)]
    [TestCase(YutResult.Do, 4)]
    [TestCase(YutResult.Gae, 5)]
    [TestCase(YutResult.Geol, 6)]
    [TestCase(YutResult.Yut, 6)]
    [TestCase(YutResult.Mo, 7)]
    public void RestoredVeteran_AppliesServerMoveWithOnlyYutMoReduced(YutResult rolled, int destination)
    {
        int moves = 0, rejected = 0;
        ObserveSnapshots(_ => { });
        sessionType.GetEvent("Moved").AddEventHandler(session, new Action<MoveResult>(_ => moves++));
        sessionType.GetEvent("RequestRejected").AddEventHandler(session, new Action<string>(_ => rejected++));
        object snapshot = PopulatedSnapshot();
        Set(snapshot, "currentTeam", 0);
        Set(snapshot, "storedResults", new List<YutResult> { rolled });
        Set(snapshot, "ownedAugments", new Dictionary<int, List<string>> { { 0, new List<string> { "Veteran" } } });
        Receive(snapshot, "GAME_SNAPSHOT");
        object move = Activator.CreateInstance(RuntimeType("MoveAppliedMessage"));
        Set(move, "team", 0); Set(move, "pieceId", 0); Set(move, "moveCount", rolled.Steps());
        Set(move, "destinationNodeId", destination);
        Set(move, "movedPieceIds", new List<int> { 0 });
        Set(move, "capturedPieceIds", destination == 5 ? new List<int> { 101 } : new List<int>());
        Receive(move, "MOVE_APPLIED", 41);
        Assert.That(moves, Is.EqualTo(1));
        Assert.That(rejected, Is.Zero);
        Assert.That(board.State.GetPiece(0).Node.Id, Is.EqualTo(destination));
    }

    [Test]
    public void RequestedSnapshotAtSameSequence_StillRepairsState()
    {
        int snapshots = 0; ObserveSnapshots(_ => snapshots++);
        Receive(PopulatedSnapshot(), "GAME_SNAPSHOT");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        sessionType.GetField("awaitingSnapshot", flags).SetValue(session, true);
        sessionType.GetField("syncRequested", flags).SetValue(session, true);
        Receive(PopulatedSnapshot(), "GAME_SNAPSHOT");
        Assert.That(snapshots, Is.EqualTo(2));
        Assert.That((bool)sessionType.GetProperty("IsCommandPending").GetValue(session), Is.False);
    }

    [Test]
    public void FirstSnapshotAtObservedSequence_StillRestoresPieces()
    {
        int snapshots = 0; ObserveSnapshots(_ => snapshots++);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        sessionType.GetField("hasReceivedSequence", flags).SetValue(session, true);
        sessionType.GetField("lastSequence", flags).SetValue(session, 40L);
        Receive(PopulatedSnapshot(), "GAME_SNAPSHOT");
        Assert.That(snapshots, Is.EqualTo(1));
        Assert.That(board.State.GetPiece(0).Node.Id, Is.EqualTo(3));
    }

    [Test]
    public void NewerSnapshot_StillRestoresServerState()
    {
        int snapshots = 0; ObserveSnapshots(_ => snapshots++);
        Receive(PopulatedSnapshot(), "GAME_SNAPSHOT");
        object newer = PopulatedSnapshot(); Set(newer, "lastSeq", 42L);
        Receive(newer, "GAME_SNAPSHOT");
        Assert.That(snapshots, Is.EqualTo(2));
    }

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
