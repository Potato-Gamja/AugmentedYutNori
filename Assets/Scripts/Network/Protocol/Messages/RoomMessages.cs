using System.Collections.Generic;


// C→S CREATE_ROOM
public class CreateRoomRequest
{
    public bool isPublic;
    public int maxPlayers;
    public int timeLimit;
}

// C→S JOIN_ROOM
public class JoinRoomRequest
{
    public string roomCode;
    public string nickname;
}

// C→S READY
public class ReadyRequest
{
    public bool ready;      // true = 준비, false = 준비 해제
}

// C→S LEAVE_ROOM 은 내용 없음 → MessageSerializer.Serialize(MessageType.LEAVE_ROOM)

// S→C ROOM_STATE  방 인원·준비 상태가 바뀔 때마다 전체를 다시 보낸다
public class RoomStateMessage
{
    public string roomCode;
    public string status;
    public int currentPlayers;
    public int maxPlayers;
    public List<RoomPlayer> members = new List<RoomPlayer>();
}

public class RoomPlayer
{
    public string playerId;
    public string nickname;
    public string iconId;     // 서버가 아직 보내지 않으면 팀별 기본 아이콘을 사용한다
    public int team;        // 0, 1 (아직 배정 전이면 -1)
    public bool ready;
}

// S→C GAME_START  필드는 Core/Game/GameSetup과 1:1로 맞춘다
public class GameStartMessage
{
    public string roomCode;
    public int teamCount;
    public int piecesPerTeam;           // MVP 2 (GameSetup 기본값 4를 덮어씀)
    public int localTeam;               // 이 기기가 조작하는 팀
    public bool allowSkipShortcut;
    public string boardLayoutId;
    public int turnTimeLimitSec;        // 서버 턴 제한시간. HUD 타이머에 사용

    public GameSetup ToGameSetup()
    {
        return new GameSetup
        {
            teamCount = teamCount,
            piecesPerTeam = piecesPerTeam,
            localTeam = localTeam,
            allowSkipShortcut = allowSkipShortcut,
            boardLayoutId = boardLayoutId,
        };
    }
}
