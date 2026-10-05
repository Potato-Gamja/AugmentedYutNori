
// 말 ID 규칙: 팀 * 100 + 순번 (GameSetup.PieceId). 서버도 같은 규칙을 쓴다.

// C→S THROW_REQUEST 는 내용 없음 → MessageSerializer.Serialize(MessageType.THROW_REQUEST)

// C→S RETHROW_REQUEST  재던지기 증강 사용
public class RethrowRequest
{
    public string augmentId;    // 사용할 증강 ID (예: "Rethrow")
}

// S→C THROW_RESULT  던지기·재던지기 결과 공통
public class ThrowResultMessage
{
    public int team;
    public bool[] sticks;       // 길이 4, true = 평평한 면, [0]이 표시된 윷(빽도 판정용)
    public YutResult result;    // "BackDo", "Do", "Gae", "Geol", "Yut", "Mo"
    public bool isRethrow;      // true면 GameRules.ApplyRethrow, false면 GameRules.ApplyThrow
}

// C→S MOVE  내가 둔 이동 + 이동 후 판단 정보
// 서버는 이 정보로 턴 종료·추가 던지기·승리를 판단
public class MoveMessage
{
    // MoveCommand
    public int pieceId;
    public int moveCount;
    public int destinationNodeId;

    // MoveReport
    public int capturedCount;
    public bool isFinished;
    public bool hasExtraThrow;
    public bool hasRemainingAction;
    public string stateHash;    // StateHasher.HashHex

    public static MoveMessage From(MoveCommand command, MoveReport report)
    {
        return new MoveMessage
        {
            pieceId = command.PieceId,
            moveCount = command.MoveCount,
            destinationNodeId = command.DestinationNodeId,
            capturedCount = report.CapturedCount,
            isFinished = report.IsFinished,
            hasExtraThrow = report.HasExtraThrow,
            hasRemainingAction = report.HasRemainingAction,
            stateHash = report.StateHash,
        };
    }

    public MoveCommand ToCommand() => new MoveCommand(pieceId, moveCount, destinationNodeId);
}

// S→C MOVE_APPLIED  서버가 확정한 이동. 양쪽 모두에게 보낸다
// 상대 기기는 BoardState.TryApply(ToCommand())로 재계산하고 stateHash로 검증
public class MoveAppliedMessage
{
    public int team;
    public int pieceId;
    public int moveCount;
    public int destinationNodeId;
    public string stateHash;    // 보낸 쪽이 계산한 해시. 다르면 desync

    public MoveCommand ToCommand() => new MoveCommand(pieceId, moveCount, destinationNodeId);
}

// S→C TURN_CHANGED
public class TurnChangedMessage
{
    public int team;                // 이제 행동할 팀 → TurnManager.ApplyServerTurn
    public int remainingThrows;
    public int turnTimeLimitSec;
}

// S→C GAME_ENDED
public class GameEndedMessage
{
    public int winnerTeam;
    public GameEndReason reason;
}

public enum GameEndReason
{
    FINISHED,       // 말이 모두 들어옴
    FORFEIT,        // 기권 / 방 나감
    DISCONNECTED,   // 재접속 유예시간 초과
}
