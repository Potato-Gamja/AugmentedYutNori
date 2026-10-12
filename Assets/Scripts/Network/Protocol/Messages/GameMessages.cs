using System.Collections.Generic;

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

// C→S MOVE  "어떤 말을, 어떤 윷 결과(칸 수)로, 어느 칸으로"만 보낸다.
// 잡기·업기·완주 같은 결과는 서버가 판으로 계산해서 MOVE_APPLIED로 돌려준다.
public class MoveMessage
{
    public int pieceId;
    public int moveCount;           // 사용할 윷 결과의 칸 수 (증강 보정 전, 빽도 = -1)
    public int destinationNodeId;   // 지름길·빽도 전환처럼 갈 길이 여러 개일 때 도착 칸
    public string stateHash;        // 서버가 MOVE_APPLIED에 그대로 실어 보낸다. 이동 전에는 알 수 없어 비워 보낸다

    public static MoveMessage From(MoveCommand command)
    {
        return new MoveMessage
        {
            pieceId = command.PieceId,
            moveCount = command.MoveCount,
            destinationNodeId = command.DestinationNodeId,
        };
    }

    public MoveCommand ToCommand() => new MoveCommand(pieceId, moveCount, destinationNodeId);
}

// S→C MOVE_APPLIED  서버가 판으로 계산해 확정한 이동. 방 전체(이동한 사람 포함)에게 보낸다.
// 모든 기기가 이 메시지를 받은 뒤 Core에 적용하고, 결과가 서버 값과 같은지 확인한다
public class MoveAppliedMessage
{
    public int team;
    public int pieceId;
    public int moveCount;
    public int destinationNodeId;
    public List<int> movedPieceIds;     // 같이 움직인 말 (업힌 말 포함)
    public List<int> capturedPieceIds;  // 잡혀서 대기석으로 간 말
    public bool isFinished;             // 움직인 말이 완주했는지
    public string stateHash;            // MOVE에 실려 온 해시 (현재 클라는 비워 보냄)
    public bool auto;                   // 끊긴 플레이어 자동 진행으로 서버가 대신 둔 이동인지

    public MoveCommand ToCommand() => new MoveCommand(pieceId, moveCount, destinationNodeId);
}

// S→C TURN_CHANGED
public class TurnChangedMessage
{
    public int team;                // 이제 행동할 팀 → TurnManager.ApplyServerTurn
    public int remainingThrows;
    public int turnTimeLimitSec;
    public int turnNumber;          // 새 턴마다 1씩 증가
}

// S→C GAME_ENDED
public class GameEndedMessage
{
    public int winnerTeam;
    public GameEndReason reason;
    public RankingEntry[] rankings = new RankingEntry[0]; // 이번 판 등수 목록 (서버가 비워 보낼 수 있음)
}

// GAME_ENDED 순위 한 줄 (플레이어 = 팀). rank는 1부터, 같은 등수 다음은 건너뜀
// 기권·끊김으로 나간 플레이어는 꼴찌 등수 + rewardGold 0
public class RankingEntry
{
    public string playerId;
    public int team;
    public int rank;
    public int rewardGold;
}

public enum GameEndReason
{
    FINISHED,       // 말이 모두 들어옴
    FORFEIT,        // 기권 / 방 나감
    DISCONNECTED,   // 재접속 유예시간 초과
}

// C→S STATE_HASH
public class StateHashReport
{
    public long seq;
    public string stateHash;
}
