using System;
using System.Collections.Generic;

/// <summary>
/// IGameSession 요청을 WebSocket 메시지로 보내고, 서버가 확정한 메시지를 Core와 화면에 반영한다.
/// WebSocket 연결 수명과 로비 메시지는 LobbyNetworkCoordinator가 담당한다.
///
/// 서버 담당 확인 사항:
/// - 방 전체 broadcast는 seq를 1씩 증가시킨다. PONG/개인 ERROR처럼 seq=0인 메시지는 순서 검사에서 제외한다.
/// - GAME_START의 teamCount는 2~4. 내 localTeam은 로비의 인증된 playerId와 ROOM_STATE 배정으로 정한다.
/// - THROW_RESULT/MOVE_APPLIED/AUGMENT_SELECTED에는 행동한 team을 반드시 포함한다.
/// - MOVE는 말 id·칸 수·도착 칸만 보낸다. 내 이동도 먼저 적용하지 않고, 서버의 MOVE_APPLIED를 받은 뒤 모든 기기가 같은 순서로 적용한다.
/// - MOVE_APPLIED의 movedPieceIds/capturedPieceIds/isFinished와 Core 적용 결과가 다르면 서버와 판이 어긋난 것으로 알린다.
/// - GAME_SNAPSHOT으로 말·증강·턴·저장 결과와 메시지 순서를 복원한다.
/// </summary>
public sealed class RemoteGameSession : IGameSession, IDisposable
{
    private const string RethrowAugmentId = "Rethrow";

    private readonly WebSocketClient socket;
    private readonly GameRules rules;
    private readonly GameEvents gameEvents;
    private readonly GameState gameState;
    private readonly TurnManager turnManager;
    private readonly GameSetup setup;

    private bool commandPending;
    private bool hasReceivedSequence;
    private bool hasAppliedSnapshot;
    private long lastSequence;
    private readonly Queue<WsEnvelope> pendingMessages;
    private bool receivingLiveMessages;

    private bool reconnectRejected;
    public bool IsCommandPending => commandPending || awaitingSnapshot || reconnectRejected;
    private bool syncRequested;   // SYNC_REQUEST를 보냈고 아직 스냅샷을 못 받음 (중복 요청 방지)
    private bool awaitingSnapshot;
    private bool hasTurnNumber;
    private int lastTurnNumber;   // 마지막으로 적용한 서버 턴 번호 (같은 턴 재전송 구분용)

    // 마지막 GAME_ENDED의 순위·보상 목록 (결과 화면용). 게임이 끝나기 전엔 빈 배열
    internal int AugmentSelectionTimeLimitSec { get; private set; }

    public IReadOnlyList<RankingEntry> Rankings { get; private set; } = Array.Empty<RankingEntry>();

    public event Action<YutThrowOutcome> Thrown;
    public event Action<MoveResult> Moved;
    public event Action<int> TurnChanged;
    public event Action<int, IReadOnlyList<string>> AugmentChoices;
    public event Action<int, IAugment> AugmentSelected;
    public event Action<int> GameEnded;
    public event Action<string> RequestRejected;
    public event Action<string, int> PlayerDisconnected;
    public event Action<string> PlayerReconnected;
    public event Action<string, int> PlayerRemoved;
    public event Action<int> AutomatedMoveApplied;
    public event Action<bool> ConnectionAvailabilityChanged;
    public event Action<GameSnapshotMessage> SnapshotApplied;
    public event Action<GameSnapshotMessage> AugmentsRestored;   // 스냅샷의 증강 복원 직후, 말 복원 전 (팀별 말 수를 정할 수 있는 시점)
    public event Action<string> ReconnectRejected;


    public RemoteGameSession(
        WebSocketClient socket,
        GameRules rules,
        GameEvents gameEvents,
        GameState gameState,
        TurnManager turnManager,
        GameSetup setup,
        IEnumerable<WsEnvelope> pendingMessages = null)
    {
        this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.gameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
        this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
        this.pendingMessages = new Queue<WsEnvelope>(pendingMessages ?? Array.Empty<WsEnvelope>());

        socket.MessageReceived += OnMessageReceived;
        socket.Connected += OnConnected;
        socket.Error += OnSocketError;
        socket.Disconnected += OnDisconnected;
    }

    public void Dispose()
    {
        socket.MessageReceived -= OnMessageReceived;
        socket.Connected -= OnConnected;
        socket.Error -= OnSocketError;
        socket.Disconnected -= OnDisconnected;
    }

    public void RequestThrow()
    {
        SendCommand(MessageType.THROW_REQUEST);
    }

    public void RequestRethrow()
    {
        SendCommand(MessageType.RETHROW_REQUEST, new RethrowRequest
        {
            augmentId = RethrowAugmentId,
        });
    }

    public void RequestAugment(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            Reject("선택할 증강 ID가 없습니다.");
            return;
        }

        if (!CanSendCommand(false)) return;
        if (!socket.Send(MessageType.SELECT_AUGMENT, new SelectAugmentRequest
        {
            augmentId = augmentId,
        }))
        {
            Reject("증강 선택을 서버에 보내지 못했습니다.");
            return;
        }
        commandPending = true;
    }

    public void RequestMove(int pieceId, int resultIndex, int destinationNodeId)
    {
        if (!CanSendCommand()) return;

        // 보내기 전에 로컬 규칙으로 갈 수 있는 이동인지만 확인한다. 실제 적용은 MOVE_APPLIED를 받은 뒤에 한다
        MoveOption? selected = FindMoveOption(pieceId, resultIndex, destinationNodeId);
        if (!selected.HasValue)
        {
            Reject("현재 상태에서 선택한 이동을 적용할 수 없습니다.");
            return;
        }

        int moveCount = gameState.StoredResults[resultIndex].Steps();
        MoveMessage message = MoveMessage.From(new MoveCommand(pieceId, moveCount, destinationNodeId));
        if (!socket.Send(MessageType.MOVE, message))
        {
            Reject("이동 요청을 서버에 보내지 못했습니다.");
            return;
        }

        commandPending = true;
    }

    private void OnMessageReceived(WsEnvelope envelope)
    {
        if (reconnectRejected) return;
        if (!receivingLiveMessages)
        {
            pendingMessages.Enqueue(envelope);
            return;
        }
        if (envelope == null) return;

        // 스냅샷은 seq 기준을 새로 잡으므로 순서 검사 전에 처리
        if (envelope.type == MessageType.GAME_SNAPSHOT)
        {
            try
            {
                GameSnapshotMessage snapshot = MessageSerializer.ReadPayload<GameSnapshotMessage>(envelope);
                // 자동 복귀 스냅샷과 SYNC_REQUEST 응답이 중복될 수 있다.
                // 이미 복원한 뒤의 과거·중복 상태로 진행 중인 이동 연출을 다시 취소하지 않는다.
                // 직접 요청한 같은 seq의 스냅샷은 DESYNC 복구를 위해 적용한다.
                if (snapshot != null && hasAppliedSnapshot &&
                    (snapshot.lastSeq < lastSequence ||
                     (snapshot.lastSeq == lastSequence && !awaitingSnapshot))) return;
                ApplySnapshot(snapshot);
            }
            catch (Exception exception)
            {
                syncRequested = false;
                awaitingSnapshot = false;
                Reject($"게임 상태 복원에 실패했습니다: {exception.Message}");
            }
            return;
        }
        if (awaitingSnapshot && envelope.seq != 0) return;
        if (!AcceptSequence(envelope)) return;
        try
        {
            switch (envelope.type)
            {
                case MessageType.THROW_RESULT:
                    ApplyThrow(MessageSerializer.ReadPayload<ThrowResultMessage>(envelope));
                    break;
                case MessageType.MOVE_APPLIED:
                    ApplyMove(MessageSerializer.ReadPayload<MoveAppliedMessage>(envelope), envelope.seq);
                    break;
                case MessageType.TURN_CHANGED:
                    ApplyTurn(MessageSerializer.ReadPayload<TurnChangedMessage>(envelope));
                    break;
                case MessageType.AUGMENT_CHOICES:
                    ApplyAugmentChoices(MessageSerializer.ReadPayload<AugmentChoicesMessage>(envelope));
                    break;
                case MessageType.AUGMENT_SELECTED:
                    ApplyAugmentSelected(MessageSerializer.ReadPayload<AugmentSelectedMessage>(envelope));
                    break;
                case MessageType.GAME_ENDED:
                    ApplyGameEnded(MessageSerializer.ReadPayload<GameEndedMessage>(envelope));
                    break;
                case MessageType.PLAYER_DISCONNECTED:
                    ApplyPlayerDisconnected(MessageSerializer.ReadPayload<PlayerDisconnectedMessage>(envelope));
                    break;
                case MessageType.PLAYER_RECONNECTED:
                    ApplyPlayerReconnected(MessageSerializer.ReadPayload<PlayerReconnectedMessage>(envelope));
                    break;
                case MessageType.PLAYER_REMOVED:
                    ApplyPlayerRemoved(MessageSerializer.ReadPayload<PlayerRemovedMessage>(envelope));
                    break;
                case MessageType.ERROR:
                    ErrorMessage error = MessageSerializer.ReadPayload<ErrorMessage>(envelope);
                    commandPending = false;
                    // 재연결 후 SYNC_REQUEST가 거절되면(게임 없음 등) 대기를 풀어 멈추지 않게 한다
                    if (awaitingSnapshot) { awaitingSnapshot = false; syncRequested = false; }
                    if (ReconnectFailure.IsTerminal(error?.code))
                    {
                        reconnectRejected = true;
                        pendingMessages.Clear();
                        ConnectionAvailabilityChanged?.Invoke(false);
                        ReconnectRejected?.Invoke(error?.message ?? "이 경기에 복귀할 수 없습니다.");
                        break;
                    }
                    Reject(error?.message ?? error?.code ?? "서버가 요청을 거절했습니다.");
                    break;
            }
        }
        catch (Exception exception)
        {
            commandPending = false;
            Reject($"서버 메시지를 적용하지 못했습니다: {exception.Message}");
            RequestSync();   // 적용에 실패하면 서버 상태로 다시 맞춘다
        }
    }

    private void ApplyThrow(ThrowResultMessage message)
    {
        if (message == null) throw new InvalidOperationException("THROW_RESULT 내용이 없습니다.");
        if (message.team != turnManager.CurrentTeam)
            throw new InvalidOperationException($"현재 팀과 던진 팀이 다릅니다. Current={turnManager.CurrentTeam}, Server={message.team}");

        YutThrowOutcome outcome;
        if (message.isRethrow)
        {
            if (!rules.ApplyRethrow(message.sticks, message.result))
                throw new InvalidOperationException("서버 재던지기 결과를 적용할 수 없습니다.");
            outcome = new YutThrowOutcome(message.sticks, message.result);
        }
        else
        {
            outcome = rules.ApplyThrow(message.sticks, message.result);
        }

        commandPending = false;
        Thrown?.Invoke(outcome);
    }

    // 서버가 확정한 이동을 내 이동·상대 이동 구분 없이 같은 방법으로 적용한다
    private void ApplyMove(MoveAppliedMessage message, long sequence)
    {
        if (message == null) throw new InvalidOperationException("MOVE_APPLIED 내용이 없습니다.");

        int resultIndex = FindStoredResultIndex(message.moveCount);
        if (resultIndex < 0)
            throw new InvalidOperationException($"{message.moveCount}칸 이동에 사용할 저장 결과가 없습니다.");

        MoveResult result = null;
        void CaptureMove(MoveResult moved) => result = moved;
        gameEvents.PieceMoved += CaptureMove;
        try
        {
            if (!rules.MovePiece(message.pieceId, resultIndex, message.destinationNodeId) || result == null)
                throw new InvalidOperationException("서버가 확정한 이동을 Core에서 재현할 수 없습니다.");
        }
        finally
        {
            gameEvents.PieceMoved -= CaptureMove;
        }

        if (message.team == setup.localTeam) commandPending = false;
        if (message.auto) AutomatedMoveApplied?.Invoke(message.team);

        Moved?.Invoke(result);

        // 연출은 Core 결과로 하지만, 서버가 계산한 결과(잡힌 말·같이 움직인 말·완주)와 같은지 확인한다
        string mismatch = FindResultMismatch(message, result);
        if (mismatch != null)
        {
            Reject($"이동 결과가 서버와 다릅니다 ({mismatch}). 상태를 다시 동기화해 주세요.");
            RequestSync();
        }
        ReportStateHash(sequence, message.stateHash);
    }

    // 서버 MOVE_APPLIED와 Core 이동 결과 비교. 같으면 null
    private static string FindResultMismatch(MoveAppliedMessage message, MoveResult result)
    {
        if (message.isFinished != result.Path.IsFinished)
            return $"완주 서버={message.isFinished}, 로컬={result.Path.IsFinished}";
        if (message.movedPieceIds != null && !SameIds(message.movedPieceIds, result.MovedPieces))
            return $"같이 움직인 말 서버=[{string.Join(",", message.movedPieceIds)}], 로컬=[{JoinIds(result.MovedPieces)}]";
        if (message.capturedPieceIds != null && !SameIds(message.capturedPieceIds, result.CapturedPieces))
            return $"잡힌 말 서버=[{string.Join(",", message.capturedPieceIds)}], 로컬=[{JoinIds(result.CapturedPieces)}]";
        return null;
    }

    private static bool SameIds(List<int> serverIds, IReadOnlyList<Piece> pieces)
    {
        if (serverIds.Count != pieces.Count) return false;
        var remaining = new HashSet<int>(serverIds);
        foreach (Piece piece in pieces)
            if (!remaining.Remove(piece.Id)) return false;
        return true;
    }

    private static string JoinIds(IReadOnlyList<Piece> pieces)
    {
        var ids = new List<int>(pieces.Count);
        foreach (Piece piece in pieces) ids.Add(piece.Id);
        return string.Join(",", ids);
    }

    // TURN_CHANGED
    //  - 새 턴(턴 번호가 바뀜): 턴 초기화 (윷 결과·이번 턴 표시 지움)
    //  - 같은 턴 재전송(시간 초과 자동 처리 후, 재접속 복귀): 남은 던지기만 맞추고 윷 결과는 유지
    private void ApplyTurn(TurnChangedMessage message)
    {
        if (message == null) throw new InvalidOperationException("TURN_CHANGED 내용이 없습니다.");

        bool sameTurn = hasTurnNumber
                        && message.turnNumber == lastTurnNumber
                        && message.team == turnManager.CurrentTeam;

        if (sameTurn)
            gameState.RemainingThrows = message.remainingThrows;
        else
            turnManager.ApplyServerTurn(message.team, message.remainingThrows);

        hasTurnNumber = true;
        lastTurnNumber = message.turnNumber;
        commandPending = false;
        TurnChanged?.Invoke(message.team);
    }

    private void ApplyAugmentChoices(AugmentChoicesMessage message)
    {
        if (message == null) throw new InvalidOperationException("AUGMENT_CHOICES 내용이 없습니다.");
        if (message.team == setup.localTeam)
            AugmentSelectionTimeLimitSec = Math.Max(0, message.timeLimitSec);
        rules.SetAugmentChoices(message.team, message.augmentIds);
        AugmentChoices?.Invoke(message.team, message.augmentIds);
    }

    private void ApplyAugmentSelected(AugmentSelectedMessage message)
    {
        if (message == null) throw new InvalidOperationException("AUGMENT_SELECTED 내용이 없습니다.");
        if (message.team < 0 || message.team >= setup.teamCount)
            throw new InvalidOperationException("증강을 선택한 팀 번호가 올바르지 않습니다.");
        IAugment augment = rules.ApplyConfirmedAugment(message.team, message.augmentId);
        if (message.team == setup.localTeam) commandPending = false;
        AugmentSelected?.Invoke(message.team, augment);
    }

    private void ApplyGameEnded(GameEndedMessage message)
    {
        if (message == null) throw new InvalidOperationException("GAME_ENDED 내용이 없습니다.");
        gameState.WinnerTeam = message.winnerTeam;
        Rankings = message.rankings ?? Array.Empty<RankingEntry>();
        commandPending = false;
        GameEnded?.Invoke(message.winnerTeam);
    }

    private void ApplyPlayerDisconnected(PlayerDisconnectedMessage message)
    {
        if (message == null || string.IsNullOrEmpty(message.playerId))
            throw new InvalidOperationException("PLAYER_DISCONNECTED 내용이 없습니다.");
        PlayerDisconnected?.Invoke(message.playerId, Math.Max(0, message.graceSec));
    }

    private void ApplyPlayerReconnected(PlayerReconnectedMessage message)
    {
        if (message == null || string.IsNullOrEmpty(message.playerId))
            throw new InvalidOperationException("PLAYER_RECONNECTED 내용이 없습니다.");
        PlayerReconnected?.Invoke(message.playerId);
    }

    // 나가기·재접속 실패로 빠진 플레이어: 그 팀 말을 판에서 지운다
    // 턴은 서버가 TURN_CHANGED로 넘겨주고, 1명만 남으면 이어서 GAME_ENDED가 온다
    private void ApplyPlayerRemoved(PlayerRemovedMessage message)
    {
        if (message == null || message.team < 0 || message.team >= setup.teamCount)
            throw new InvalidOperationException("PLAYER_REMOVED 내용이 없습니다.");

        gameState.Board.RemoveTeam(message.team);
        PlayerRemoved?.Invoke(message.playerId, message.team);
    }

    private void ReportStateHash(long sequence, string serverHash)
    {
        string localHash = StateHasher.HashHex(gameState, turnManager);
        socket.Send(MessageType.STATE_HASH, new StateHashReport
        {
            seq = sequence,
            stateHash = localHash,
        });

        if (!string.IsNullOrEmpty(serverHash) && !string.Equals(localHash, serverHash, StringComparison.Ordinal))
            Reject($"게임 상태가 서버와 다릅니다. Local={localHash}, Server={serverHash}");
    }

    private MoveOption? FindMoveOption(int pieceId, int resultIndex, int destinationNodeId)
    {
        foreach (MoveOption option in rules.GetMoveOptions(pieceId))
        {
            if (option.ResultIndex == resultIndex && option.DestinationNodeId == destinationNodeId)
                return option;
        }
        return null;
    }

    private int FindStoredResultIndex(int moveCount)
    {
        for (int i = 0; i < gameState.StoredResults.Count; i++)
        {
            if (gameState.StoredResults[i].Steps() == moveCount) return i;
        }
        return -1;
    }

    private bool AcceptSequence(WsEnvelope envelope)
    {
        // seq 0은 PONG/ERROR 같은 개인 메시지이므로 방 broadcast 순서 검사에서 제외한다.
        // 재접속 시 첫 broadcast seq가 1보다 클 수 있어 첫 양수 seq는 기준값으로 받아들인다.
        // 서버가 GAME_START에 기준 seq를 추가하면 이 초기화도 해당 값으로 교체할 것.
        if (envelope.seq == 0) return true;

        if (!hasReceivedSequence)
        {
            hasReceivedSequence = true;
            lastSequence = envelope.seq;
            return true;
        }

        if (envelope.seq <= lastSequence) return false;
        if (envelope.seq != lastSequence + 1)
        {
            Reject($"서버 메시지 순서가 끊겼습니다. Last={lastSequence}, Received={envelope.seq}");
            RequestSync();
            return false;
        }

        lastSequence = envelope.seq;
        return true;
    }

    private void SendCommand(MessageType type)
    {
        if (!CanSendCommand()) return;
        if (!socket.Send(type))
        {
            Reject("요청을 서버에 보내지 못했습니다.");
            return;
        }
        commandPending = true;
    }

    private void SendCommand<T>(MessageType type, T payload)
    {
        if (!CanSendCommand()) return;
        if (!socket.Send(type, payload))
        {
            Reject("요청을 서버에 보내지 못했습니다.");
            return;
        }
        commandPending = true;
    }

    // Flow가 말과 HUD, 이벤트 구독을 준비한 뒤 씬 전환 중 받은 메시지를 순서대로 적용한다.
    public void ReplayPendingMessages()
    {
        receivingLiveMessages = true;
        while (pendingMessages.Count > 0) OnMessageReceived(pendingMessages.Dequeue());
    }

    private bool CanSendCommand(bool requireCurrentTurn = true)
    {
        if (IsCommandPending)
        {
            Reject("이전 요청의 서버 응답을 기다리고 있습니다.");
            return false;
        }
        if (!socket.IsConnected)
        {
            Reject("서버에 연결되어 있지 않습니다.");
            return false;
        }
        if (requireCurrentTurn && !setup.CanControl(turnManager.CurrentTeam))
        {
            Reject("현재 조작할 수 있는 팀의 차례가 아닙니다.");
            return false;
        }
        return true;
    }

    private void OnSocketError(string message)
    {
        commandPending = false;
        Reject($"서버 연결 오류: {message}");
    }

    private void OnConnected()
    {
        if (reconnectRejected) return;
        // 게임 중 재연결이면 스냅샷을 받을 때까지 seq 메시지를 무시한다
        // 서버가 재접속 시 자동으로 보내지만, 혹시 못 받는 경우를 대비해 직접도 요청한다 (중복 스냅샷은 무해)
        if (hasReceivedSequence)
        {
            awaitingSnapshot = true;
            syncRequested = false;
            RequestSync();
        }
        ConnectionAvailabilityChanged?.Invoke(true);
    }

    private void OnDisconnected(bool byUser)
    {
        if (reconnectRejected) return;
        commandPending = false;
        ConnectionAvailabilityChanged?.Invoke(false);
        if (!byUser) Reject("서버 연결이 끊어졌습니다.");
    }

    private void Reject(string message) => RequestRejected?.Invoke(message);

    // 서버 상태로 통째로 덮어쓴다: 증강 → 말 위치 → 턴·윷 결과 → 승자 → seq 기준
    private void ApplySnapshot(GameSnapshotMessage snapshot)
    {
        if (snapshot == null) throw new InvalidOperationException("GAME_SNAPSHOT 내용이 없습니다.");

        // 증강 먼저: 노장이면 말 수가 달라서, 증강을 알아야 말을 만들 수 있다
        RestoreAugments(snapshot.ownedAugments);
        AugmentsRestored?.Invoke(snapshot);
        RestorePieces(snapshot.pieces);

        bool ended = snapshot.phase == "ENDED";
        if (!ended) turnManager.ApplyServerTurn(snapshot.currentTeam, snapshot.remainingThrows);

        // ApplyServerTurn 뒤에 넣어야 턴 적용이 결과 목록을 건드려도 서버 값이 남는다
        gameState.StoredResults.Clear();
        if (snapshot.storedResults != null) gameState.StoredResults.AddRange(snapshot.storedResults);
        gameState.WinnerTeam = snapshot.winnerTeam;

        // 증강 선택 중이면 내 후보를 다시 등록 (선택 요청이 후보 검사를 통과하도록)
        if (snapshot.phase == "AUGMENT_SELECT" && snapshot.offeredAugments != null && snapshot.offeredAugments.Count > 0)
            rules.SetAugmentChoices(snapshot.myTeam, snapshot.offeredAugments);

        hasReceivedSequence = true;
        lastSequence = snapshot.lastSeq;
        hasTurnNumber = true;
        lastTurnNumber = snapshot.turnNumber;
        commandPending = false;
        syncRequested = false;
        awaitingSnapshot = false;

        hasAppliedSnapshot = true;
        SnapshotApplied?.Invoke(snapshot);
    }

    // 서버 말 상태로 맞추고, 서버 판에 없는 팀(나간 팀)은 판에서 지운다
    private void RestorePieces(List<PieceSnapshot> pieces)
    {
        if (pieces == null) throw new InvalidOperationException("스냅샷의 말 목록이 없습니다.");

        var validatedIds = new HashSet<int>();
        foreach (PieceSnapshot p in pieces)
        {
            Piece piece = p == null ? null : gameState.Board.GetPiece(p.pieceId);
            if (piece == null || piece.Team != p.team || !validatedIds.Add(p.pieceId))
                throw new InvalidOperationException("스냅샷의 말 번호 또는 팀 정보가 올바르지 않습니다.");
            PieceState state = ParsePieceState(p.state);
            if ((state == PieceState.OnBoard && gameState.Board.Graph.GetNode(p.nodeId) == null) ||
                (p.cameFromNodeId.HasValue && gameState.Board.Graph.GetNode(p.cameFromNodeId.Value) == null))
                throw new InvalidOperationException($"말 {p.pieceId}의 복원 칸이 판에 없습니다.");
        }

        var listed = new HashSet<int>();
        foreach (PieceSnapshot p in pieces)
        {
            if (!gameState.Board.RestorePiece(p.pieceId, ParsePieceState(p.state), p.nodeId, p.cameFromNodeId))
                throw new InvalidOperationException($"말 {p.pieceId}을(를) 복원할 수 없습니다 (칸 {p.nodeId}).");
            listed.Add(p.pieceId);
        }

        var removedTeams = new HashSet<int>();
        foreach (Piece piece in new List<Piece>(gameState.Board.Pieces))
            if (!listed.Contains(piece.Id)) removedTeams.Add(piece.Team);
        foreach (int team in removedTeams)
            gameState.Board.RemoveTeam(team);
    }

    // 서버가 가진 증강 중 로컬에 없는 것만 추가한다 (서버 확정 증강 적용과 같은 경로)
    private void RestoreAugments(Dictionary<int, List<string>> owned)
    {
        if (owned == null) return;
        foreach (KeyValuePair<int, List<string>> pair in owned)
        {
            var have = new HashSet<string>();
            foreach (IAugment augment in gameState.Board.GetAugments(pair.Key)) have.Add(augment.Id);

            foreach (string id in pair.Value)
            {
                if (have.Contains(id)) continue;
                rules.ApplyConfirmedAugment(pair.Key, id);
                have.Add(id);
            }
        }
    }

    private static PieceState ParsePieceState(string state)
    {
        switch (state)
        {
            case "WAITING": return PieceState.Waiting;
            case "ON_BOARD": return PieceState.OnBoard;
            case "FINISHED": return PieceState.Finished;
            default: throw new InvalidOperationException($"알 수 없는 말 상태: {state}");
        }
    }

    // 서버에 현재 상태 스냅샷을 요청한다 (순서 끊김·이동 결과 불일치·적용 실패 때 자동 호출, 버튼에서 수동 호출 가능)
    public void RequestSync()
    {
        if (reconnectRejected || syncRequested || !socket.IsConnected) return;
        if (socket.Send(MessageType.SYNC_REQUEST))
        {
            syncRequested = true;
            awaitingSnapshot = true;
            commandPending = false;
        }
    }
}
