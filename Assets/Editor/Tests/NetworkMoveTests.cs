using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// MOVE / MOVE_APPLIED / TURN_CHANGED 서버 연동 테스트 (서버 없이 실행)
// 1) 메시지 형식: 서버(Jackson)가 보내는 JSON을 읽을 수 있는지, 보내는 MOVE에 서버가 받는 필드만 들어가는지
// 2) 서버 결과 비교: MOVE_APPLIED의 잡힌 말·같이 움직인 말·완주와 Core 결과 비교
// 3) 적용 흐름: 서버 메시지를 RemoteGameSession에 넣었을 때 내 이동·상대 이동이 Core에 같은 방법으로 적용되는지
// RemoteGameSession은 Assembly-CSharp라 Core.Tests(asmdef)에서 볼 수 없어 Editor 폴더에 둔다.
// 소켓 연결 없이 확인하려고 수신 처리(OnMessageReceived)와 결과 비교(FindResultMismatch)는 리플렉션으로 부른다.
public class NetworkMoveTests
{
    private const string DefaultBoardPath = "Assets/Data/BoardLayouts/BoardLayout_Default.asset";
    private static readonly bool[] GaeSticks = { false, true, true, false };   // 평평한 면 2개 = 개 (2칸)

    // ───────────── 1. 메시지 형식 ─────────────

    [Test]
    public void Move_Serialize_HasOnlyServerFields()
    {
        string json = MessageSerializer.Serialize(MessageType.MOVE, MoveMessage.From(new MoveCommand(101, 3, 23)));
        JObject root = JObject.Parse(json);
        JObject payload = (JObject)root["payload"];

        Assert.AreEqual("MOVE", (string)root["type"]);
        Assert.AreEqual(101, (int)payload["pieceId"]);
        Assert.AreEqual(3, (int)payload["moveCount"]);
        Assert.AreEqual(23, (int)payload["destinationNodeId"]);

        // 예전 MoveReport 필드는 더 이상 보내지 않는다. stateHash는 비어 있어 생략된다 (null 필드 생략 규칙)
        var names = new List<string>();
        foreach (JProperty property in payload.Properties()) names.Add(property.Name);
        CollectionAssert.AreEquivalent(new[] { "pieceId", "moveCount", "destinationNodeId" }, names);
    }

    [Test]
    public void Move_BackDo_SendsMinusOne()
    {
        string json = MessageSerializer.Serialize(MessageType.MOVE, MoveMessage.From(new MoveCommand(0, -1, 20)));
        Assert.AreEqual(-1, (int)JObject.Parse(json)["payload"]["moveCount"]);
    }

    [Test]
    public void MoveApplied_ReadsServerJson()
    {
        // 서버 GameMessages.MoveAppliedMessage가 보내는 모양 그대로 (isFinished는 @JsonProperty로 이름 고정)
        const string json = @"{""type"":""MOVE_APPLIED"",""seq"":7,""payload"":{
            ""team"":1,""pieceId"":101,""moveCount"":3,""destinationNodeId"":3,
            ""movedPieceIds"":[101,102],""capturedPieceIds"":[0],""isFinished"":true,""stateHash"":""abc""}}";

        WsEnvelope envelope = MessageSerializer.Deserialize(json);
        MoveAppliedMessage message = MessageSerializer.ReadPayload<MoveAppliedMessage>(envelope);

        Assert.AreEqual(MessageType.MOVE_APPLIED, envelope.type);
        Assert.AreEqual(7, envelope.seq);
        Assert.AreEqual(1, message.team);
        Assert.AreEqual(101, message.pieceId);
        Assert.AreEqual(3, message.moveCount);
        Assert.AreEqual(3, message.destinationNodeId);
        CollectionAssert.AreEqual(new[] { 101, 102 }, message.movedPieceIds);
        CollectionAssert.AreEqual(new[] { 0 }, message.capturedPieceIds);
        Assert.IsTrue(message.isFinished);
        Assert.AreEqual("abc", message.stateHash);
    }

    [Test]
    public void MoveApplied_NullStateHashAndEmptyLists_Read()
    {
        const string json = @"{""type"":""MOVE_APPLIED"",""seq"":1,""payload"":{
            ""team"":0,""pieceId"":0,""moveCount"":2,""destinationNodeId"":2,
            ""movedPieceIds"":[0],""capturedPieceIds"":[],""isFinished"":false}}";

        MoveAppliedMessage message = MessageSerializer.ReadPayload<MoveAppliedMessage>(MessageSerializer.Deserialize(json));

        Assert.IsNull(message.stateHash);
        Assert.IsNotNull(message.capturedPieceIds);
        Assert.AreEqual(0, message.capturedPieceIds.Count);
    }

    [Test]
    public void TurnChanged_ReadsTurnNumber()
    {
        const string json = @"{""type"":""TURN_CHANGED"",""seq"":3,""payload"":{
            ""team"":1,""remainingThrows"":1,""turnTimeLimitSec"":30,""turnNumber"":5}}";

        TurnChangedMessage message = MessageSerializer.ReadPayload<TurnChangedMessage>(MessageSerializer.Deserialize(json));

        Assert.AreEqual(1, message.team);
        Assert.AreEqual(1, message.remainingThrows);
        Assert.AreEqual(30, message.turnTimeLimitSec);
        Assert.AreEqual(5, message.turnNumber);
    }

    [Test]
    public void ServerOnlyFields_Ignored()
    {
        // 서버가 필드를 더 추가해도 클라가 깨지지 않아야 한다
        const string json = @"{""type"":""TURN_CHANGED"",""seq"":3,""payload"":{
            ""team"":0,""remainingThrows"":1,""turnTimeLimitSec"":30,""turnNumber"":2,""futureField"":""x""}}";

        Assert.DoesNotThrow(() => MessageSerializer.ReadPayload<TurnChangedMessage>(MessageSerializer.Deserialize(json)));
    }

    // ───────────── 2. 서버 결과 비교 ─────────────

    [Test]
    public void Mismatch_SameResult_ReturnsNull()
    {
        var game = new TestGame();
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 2);

        Assert.IsNull(FindMismatch(Applied(0, 0, 2, moved: new[] { 0 }, captured: new int[0], finished: false), result));
    }

    [Test]
    public void Mismatch_DifferentOrder_ReturnsNull()
    {
        // 0번·1번 말을 같은 칸에 올려 업은 뒤, 0번 말로 이동하면 둘이 같이 움직인다
        var game = new TestGame();
        game.MoveLocally(pieceId: 0, destination: 2);
        game.EndTurnTo(0);
        game.MoveLocally(pieceId: 1, destination: 2);
        game.EndTurnTo(0);
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 4);
        Assert.AreEqual(2, result.MovedPieces.Count, "업힌 말이 같이 움직여야 테스트가 의미 있다");

        // 서버는 순서를 바꿔 보내도 같은 결과로 본다
        var reversed = new List<int>();
        foreach (Piece piece in result.MovedPieces) reversed.Insert(0, piece.Id);
        MoveAppliedMessage applied = Applied(0, 0, 2, moved: reversed.ToArray(), captured: new int[0], finished: false);

        Assert.IsNull(FindMismatch(applied, result));
    }

    [Test]
    public void Mismatch_Captured_Detected()
    {
        var game = new TestGame();
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 2);

        string mismatch = FindMismatch(Applied(0, 0, 2, moved: new[] { 0 }, captured: new[] { 100 }, finished: false), result);

        StringAssert.Contains("잡힌 말", mismatch);
    }

    [Test]
    public void Mismatch_MovedPieces_Detected()
    {
        var game = new TestGame();
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 2);

        string mismatch = FindMismatch(Applied(0, 0, 2, moved: new[] { 0, 1 }, captured: new int[0], finished: false), result);

        StringAssert.Contains("같이 움직인 말", mismatch);
    }

    [Test]
    public void Mismatch_Finished_Detected()
    {
        var game = new TestGame();
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 2);

        string mismatch = FindMismatch(Applied(0, 0, 2, moved: new[] { 0 }, captured: new int[0], finished: true), result);

        StringAssert.Contains("완주", mismatch);
    }

    [Test]
    public void Mismatch_ListsMissing_SkipsListCheck()
    {
        // 목록이 없는 메시지(예전 서버)는 목록 비교를 건너뛴다
        var game = new TestGame();
        MoveResult result = game.MoveLocally(pieceId: 0, destination: 2);

        Assert.IsNull(FindMismatch(Applied(0, 0, 2, moved: null, captured: null, finished: false), result));
    }

    // ───────────── 3. RemoteGameSession 적용 흐름 ─────────────

    [Test]
    public void RequestMove_DoesNotApplyBeforeServerConfirms()
    {
        using var game = new TestGame(withSession: true);
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 0, sticks = GaeSticks, result = YutResult.Gae });

        game.Session.RequestMove(0, 0, 2);

        // 소켓이 연결되지 않아 거절되지만, 중요한 건 Core가 먼저 바뀌지 않았다는 것
        Assert.IsTrue(game.Rules.GetMoveOptions(0).Count > 0);
        Assert.IsTrue(game.Board.GetPiece(0).IsWaiting);
        Assert.AreEqual(1, game.State.StoredResults.Count);
        Assert.AreEqual(0, game.MovedResults.Count);
        Assert.IsNotEmpty(game.Rejections);
    }

    [Test]
    public void MoveApplied_OwnMove_AppliedToCore()
    {
        using var game = new TestGame(withSession: true);
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 0, sticks = GaeSticks, result = YutResult.Gae });

        game.Receive(MessageType.MOVE_APPLIED, Applied(0, 0, 2, moved: new[] { 0 }, captured: new int[0], finished: false));

        Piece piece = game.Board.GetPiece(0);
        Assert.IsTrue(piece.IsOnBoard);
        Assert.AreEqual(2, piece.Node.Id);
        Assert.AreEqual(0, game.State.StoredResults.Count);
        Assert.AreEqual(1, game.MovedResults.Count);
        Assert.AreSame(piece, game.MovedResults[0].Piece);
        Assert.IsEmpty(game.Rejections);
        Assert.IsEmpty(game.AutomatedTeams);    // 내 이동이라 자동 진행 알림 없음
        Assert.IsFalse(game.Session.IsCommandPending);
    }

    [Test]
    public void MoveApplied_OpponentCapture_AppliedLikeOwnMove()
    {
        using var game = new TestGame(withSession: true);
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 0, sticks = GaeSticks, result = YutResult.Gae });
        game.Receive(MessageType.MOVE_APPLIED, Applied(0, 0, 2, moved: new[] { 0 }, captured: new int[0], finished: false));

        game.Receive(MessageType.TURN_CHANGED, new TurnChangedMessage { team = 1, remainingThrows = 1, turnTimeLimitSec = 30, turnNumber = 2 });
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 1, sticks = GaeSticks, result = YutResult.Gae });
        game.Receive(MessageType.MOVE_APPLIED, Applied(1, 100, 2, moved: new[] { 100 }, captured: new[] { 0 }, finished: false));

        Assert.AreEqual(2, game.Board.GetPiece(100).Node.Id);
        Assert.IsTrue(game.Board.GetPiece(0).IsWaiting, "잡힌 내 말은 대기석으로 돌아가야 한다");
        Assert.AreEqual(2, game.MovedResults.Count);
        Assert.AreEqual(1, game.MovedResults[1].CapturedPieces.Count);
        CollectionAssert.AreEqual(new[] { 1 }, game.AutomatedTeams);
        Assert.IsEmpty(game.Rejections);
    }

    [Test]
    public void MoveApplied_ServerDisagrees_StillAppliesAndReports()
    {
        using var game = new TestGame(withSession: true);
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 0, sticks = GaeSticks, result = YutResult.Gae });

        // 서버는 상대 말을 잡았다고 하는데 로컬 판에는 잡을 말이 없다 → 판이 어긋남
        game.Receive(MessageType.MOVE_APPLIED, Applied(0, 0, 2, moved: new[] { 0 }, captured: new[] { 100 }, finished: false));

        Assert.AreEqual(1, game.MovedResults.Count, "연출은 그대로 진행한다");
        Assert.AreEqual(1, game.Rejections.Count);
        StringAssert.Contains("이동 결과가 서버와 다릅니다", game.Rejections[0]);
    }

    [Test]
    public void MoveApplied_UnknownMoveCount_Rejected()
    {
        using var game = new TestGame(withSession: true);
        game.Receive(MessageType.THROW_RESULT, new ThrowResultMessage { team = 0, sticks = GaeSticks, result = YutResult.Gae });

        game.Receive(MessageType.MOVE_APPLIED, Applied(0, 0, 3, moved: new[] { 0 }, captured: new int[0], finished: false));

        Assert.AreEqual(0, game.MovedResults.Count);
        Assert.IsTrue(game.Board.GetPiece(0).IsWaiting);
        Assert.AreEqual(1, game.Rejections.Count);
        StringAssert.Contains("저장 결과가 없습니다", game.Rejections[0]);
    }

    // ───────────── 도우미 ─────────────

    private static MoveAppliedMessage Applied(int team, int pieceId, int moveCount, int[] moved, int[] captured, bool finished)
    {
        int destination = moveCount;   // 기본 판은 출발 후 n칸 = n번 칸
        return new MoveAppliedMessage
        {
            team = team,
            pieceId = pieceId,
            moveCount = moveCount,
            destinationNodeId = destination,
            movedPieceIds = moved != null ? new List<int>(moved) : null,
            capturedPieceIds = captured != null ? new List<int>(captured) : null,
            isFinished = finished,
        };
    }

    private static string FindMismatch(MoveAppliedMessage message, MoveResult result)
    {
        MethodInfo method = typeof(RemoteGameSession).GetMethod("FindResultMismatch", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "RemoteGameSession.FindResultMismatch를 찾을 수 없습니다.");
        return (string)method.Invoke(null, new object[] { message, result });
    }

    // 기본 판(BoardLayout_Default)에 2팀 × 말 2개로 Core와 RemoteGameSession을 만든다. 이 기기는 0팀
    private sealed class TestGame : System.IDisposable
    {
        public BoardState Board { get; }
        public GameState State { get; }
        public TurnManager Turn { get; }
        public GameEvents Events { get; }
        public GameRules Rules { get; }
        public RemoteGameSession Session { get; }

        public List<MoveResult> MovedResults { get; } = new List<MoveResult>();
        public List<string> Rejections { get; } = new List<string>();
        public List<int> AutomatedTeams { get; } = new List<int>();

        private readonly GameObject socketObject;
        private readonly MethodInfo onMessageReceived;

        public TestGame(bool withSession = false)
        {
            var layout = AssetDatabase.LoadAssetAtPath<BoardLayoutSO>(DefaultBoardPath);
            Assert.IsNotNull(layout, $"{DefaultBoardPath}가 없습니다.");

            Board = new BoardState(layout.Definition.CreateGraph());
            State = new GameState(Board);
            Turn = new TurnManager(State, 2);
            Events = new GameEvents();
            Rules = new GameRules(State, Turn, Events, new YutThrower(new SystemRandomProvider()),
                new StandardGetResult(), new AugmentPool(new SystemRandomProvider()));

            for (int team = 0; team < 2; team++)
                for (int slot = 0; slot < 2; slot++)
                    Board.AddPiece(GameSetup.PieceId(team, slot), team);

            if (!withSession) return;

            socketObject = new GameObject("TestSocket");
            var socket = socketObject.AddComponent<WebSocketClient>();
            var setup = new GameSetup { teamCount = 2, piecesPerTeam = 2, localTeam = 0 };
            Session = new RemoteGameSession(socket, Rules, Events, State, Turn, setup);
            Session.Moved += MovedResults.Add;
            Session.RequestRejected += Rejections.Add;
            Session.AutomatedMoveApplied += AutomatedTeams.Add;

            onMessageReceived = typeof(RemoteGameSession).GetMethod("OnMessageReceived", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(onMessageReceived, "RemoteGameSession.OnMessageReceived를 찾을 수 없습니다.");
        }

        // 서버가 보낸 메시지를 실제 JSON 왕복을 거쳐 넣는다 (seq 0 = 순서 검사 제외)
        public void Receive<T>(MessageType type, T payload)
        {
            WsEnvelope envelope = MessageSerializer.Deserialize(MessageSerializer.Serialize(type, payload));
            onMessageReceived.Invoke(Session, new object[] { envelope });
        }

        // 세션 없이 Core에서 직접 개(2칸)를 던지고 이동해 MoveResult를 얻는다
        public MoveResult MoveLocally(int pieceId, int destination)
        {
            Rules.ApplyThrow(GaeSticks, YutResult.Gae);
            MoveResult result = null;
            void Capture(MoveResult moved) => result = moved;
            Events.PieceMoved += Capture;
            try
            {
                Assert.IsTrue(Rules.MovePiece(pieceId, 0, destination), $"말 {pieceId} → {destination}번 칸 이동 실패");
            }
            finally
            {
                Events.PieceMoved -= Capture;
            }
            return result;
        }

        public void EndTurnTo(int team) => Turn.ApplyServerTurn(team, 1);

        public void Dispose()
        {
            Session?.Dispose();
            if (socketObject != null) Object.DestroyImmediate(socketObject);
        }
    }
}
