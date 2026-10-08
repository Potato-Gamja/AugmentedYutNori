// ── 시스템 메시지 ──
// Ping/Pong은 UnityEngine.Ping과 이름이 겹치지 않도록 Message를 붙인다

// C→S PING
using System.Collections.Generic;

public class PingMessage
{
    public long clientTime;     // 보낸 시각(ms). PONG에 그대로 돌아오면 왕복 지연 계산
}

// S→C PONG
public class PongMessage
{
    public long clientTime;     // PING에서 받은 값 그대로
    public long serverTime;     // 서버 현재 시각(ms)
}

// S→C ERROR  요청 거부·오류
public class ErrorMessage
{
    public string code;         // 예: "NOT_YOUR_TURN", "ROOM_NOT_FOUND", "INVALID_AUGMENT"
    public string message;      // 사람이 읽는 설명 (로그·토스트용)
}

// S→C PLAYER_DISCONNECTED
public class PlayerDisconnectedMessage
{
    public string playerId;
    public int graceSec;        // 재접속 대기 시간(s)
}

// S→C PLAYER_RECONNECTED
public class PlayerReconnectedMessage
{
    public string playerId;
}

// S→C GAME_SNAPSHOT  재접속·DESYNC 때 현재 게임 상태
public class GameSnapshotMessage
{
    public long lastSeq;        // 이 다음 seq부터 순서 검사
    public string phase;        // "AUGMENT_SELECT", "PLAYING", "ENDED"
    public int myTeam;
    public int currentTeam;
    public int remainingThrows;
    public int turnNumber;
    public List<YutResult> storedResults = new List<YutResult>();
    public Dictionary<int, List<string>> ownedAugments = new Dictionary<int, List<string>>();
    public List<string> offeredAugments = new List<string>();   // 증강 선택 중일 때 내 후보
    public int? winnerTeam;
    public string timerType;        // "TURN", "AUGMENT_SELECT", 없으면 null
    public int timerRemainingSec;
}
