using System;
using System.Collections.Generic;

/// <summary>
/// IGameSession 요청을 WebSocket 메시지로 보내고, 서버가 확정한 메시지를 Core와 화면에 반영한다.
/// WebSocket 연결 수명과 로비 메시지는 LobbyNetworkCoordinator가 담당한다.
///
/// 서버 담당 확인 사항:
/// - 방 전체 broadcast는 seq를 1씩 증가시킨다. PONG/개인 ERROR처럼 seq=0인 메시지는 순서 검사에서 제외한다.
/// - GAME_START의 teamCount는 2~4, localTeam은 0~teamCount-1로 보낸다.
/// - THROW_RESULT/MOVE_APPLIED/AUGMENT_SELECTED에는 행동한 team을 반드시 포함한다.
/// - MOVE는 현재 클라이언트 선적용 결과(MoveReport)를 전송하고, 서버의 MOVE_APPLIED를 최종 확정으로 취급한다.
/// - MOVE 거절 또는 seq 누락 뒤 완전 복구는 향후 snapshot/RECONNECT 메시지 계약이 필요하다.
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
    private long lastSequence;
    private MoveResult pendingLocalMove;
    private int pendingMovePieceId = -1;
    private int pendingMoveDestinationId = -1;

    public bool IsCommandPending => commandPending;

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

    public RemoteGameSession(
        WebSocketClient socket,
        GameRules rules,
        GameEvents gameEvents,
        GameState gameState,
        TurnManager turnManager,
        GameSetup setup)
    {
        this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.gameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
        this.setup = setup ?? throw new ArgumentNullException(nameof(setup));

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

        SendCommand(MessageType.SELECT_AUGMENT, new SelectAugmentRequest
        {
            augmentId = augmentId,
        });
    }

    public void RequestMove(int pieceId, int resultIndex, int destinationNodeId)
    {
        if (!CanSendCommand()) return;

        MoveOption? selected = FindMoveOption(pieceId, resultIndex, destinationNodeId);
        if (!selected.HasValue)
        {
            Reject("현재 상태에서 선택한 이동을 적용할 수 없습니다.");
            return;
        }

        MoveResult result = null;
        void CaptureMove(MoveResult moved) => result = moved;

        gameEvents.PieceMoved += CaptureMove;
        bool moved;
        try
        {
            // 현재 서버 계약의 MOVE에는 이동 후 MoveReport가 포함된다.
            // 따라서 요청 측 Core에 먼저 적용해 보고 보고서를 만든 뒤 전송한다.
            // 서버 거절 뒤 상태 복구는 RECONNECT/snapshot 계약이 추가될 때 이 경계에서 처리한다.
            moved = rules.MovePiece(pieceId, resultIndex, destinationNodeId);
        }
        finally
        {
            gameEvents.PieceMoved -= CaptureMove;
        }

        if (!moved || result == null)
        {
            Reject("선택한 말을 이동할 수 없습니다.");
            return;
        }

        MoveReport report = rules.BuildMoveReport(result);
        MoveMessage message = MoveMessage.From(result.ToCommand(), report);
        if (!socket.Send(MessageType.MOVE, message))
        {
            Reject("이동 요청을 서버에 보내지 못했습니다. 상태를 다시 동기화해 주세요.");
            return;
        }

        pendingLocalMove = result;
        pendingMovePieceId = pieceId;
        pendingMoveDestinationId = destinationNodeId;
        commandPending = true;
    }

    private void OnMessageReceived(WsEnvelope envelope)
    {
        if (envelope == null || !AcceptSequence(envelope)) return;

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
                // 서버/프로토콜 담당 연결 지점:
                // MessageType.PLAYER_REMOVED와 PlayerRemovedMessage(playerId, team, reason)가 추가되면
                // 여기서 PlayerRemoved?.Invoke(message.playerId, message.team)를 호출한다.
                case MessageType.ERROR:
                    ErrorMessage error = MessageSerializer.ReadPayload<ErrorMessage>(envelope);
                    commandPending = false;
                    Reject(error?.message ?? error?.code ?? "서버가 요청을 거절했습니다.");
                    break;
            }
        }
        catch (Exception exception)
        {
            commandPending = false;
            Reject($"서버 메시지를 적용하지 못했습니다: {exception.Message}");
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

    private void ApplyMove(MoveAppliedMessage message, long sequence)
    {
        if (message == null) throw new InvalidOperationException("MOVE_APPLIED 내용이 없습니다.");

        MoveResult result;
        bool isPendingLocalMove = pendingLocalMove != null &&
                                  message.team == setup.localTeam &&
                                  message.pieceId == pendingMovePieceId &&
                                  message.destinationNodeId == pendingMoveDestinationId;

        if (isPendingLocalMove)
        {
            result = pendingLocalMove;
            ClearPendingMove();
            commandPending = false;
        }
        else
        {
            int resultIndex = FindStoredResultIndex(message.moveCount);
            if (resultIndex < 0)
                throw new InvalidOperationException($"{message.moveCount}칸 이동에 사용할 저장 결과가 없습니다.");

            result = null;
            void CaptureMove(MoveResult moved) => result = moved;
            gameEvents.PieceMoved += CaptureMove;
            try
            {
                if (!rules.MovePiece(message.pieceId, resultIndex, message.destinationNodeId))
                    throw new InvalidOperationException("서버가 확정한 이동을 Core에서 재현할 수 없습니다.");
            }
            finally
            {
                gameEvents.PieceMoved -= CaptureMove;
            }
        }

        if (message.team != setup.localTeam) AutomatedMoveApplied?.Invoke(message.team);
        Moved?.Invoke(result);
        ReportStateHash(sequence, message.stateHash);
    }

    private void ApplyTurn(TurnChangedMessage message)
    {
        if (message == null) throw new InvalidOperationException("TURN_CHANGED 내용이 없습니다.");
        turnManager.ApplyServerTurn(message.team, message.remainingThrows);
        commandPending = false;
        ClearPendingMove();
        TurnChanged?.Invoke(message.team);
    }

    private void ApplyAugmentChoices(AugmentChoicesMessage message)
    {
        if (message == null) throw new InvalidOperationException("AUGMENT_CHOICES 내용이 없습니다.");
        rules.SetAugmentChoices(message.team, message.augmentIds);
        AugmentChoices?.Invoke(message.team, message.augmentIds);
    }

    private void ApplyAugmentSelected(AugmentSelectedMessage message)
    {
        if (message == null) throw new InvalidOperationException("AUGMENT_SELECTED 내용이 없습니다.");
        IAugment augment = rules.SelectAugment(message.team, message.augmentId);
        if (message.team == setup.localTeam) commandPending = false;
        AugmentSelected?.Invoke(message.team, augment);
    }

    private void ApplyGameEnded(GameEndedMessage message)
    {
        if (message == null) throw new InvalidOperationException("GAME_ENDED 내용이 없습니다.");
        gameState.WinnerTeam = message.winnerTeam;
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

    private bool CanSendCommand()
    {
        if (commandPending)
        {
            Reject("이전 요청의 서버 응답을 기다리고 있습니다.");
            return false;
        }
        if (!socket.IsConnected)
        {
            Reject("서버에 연결되어 있지 않습니다.");
            return false;
        }
        if (!setup.CanControl(turnManager.CurrentTeam))
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
        ConnectionAvailabilityChanged?.Invoke(true);
    }

    private void OnDisconnected(bool byUser)
    {
        commandPending = false;
        ClearPendingMove();
        ConnectionAvailabilityChanged?.Invoke(false);
        if (!byUser) Reject("서버 연결이 끊어졌습니다.");
    }

    private void ClearPendingMove()
    {
        pendingLocalMove = null;
        pendingMovePieceId = -1;
        pendingMoveDestinationId = -1;
    }

    private void Reject(string message) => RequestRejected?.Invoke(message);
}
