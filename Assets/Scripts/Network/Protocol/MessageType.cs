// 서버와 주고받는 메시지 종류.
// JSON에는 숫자가 아니라 이름 문자열("THROW_RESULT")로 들어간다 (MessageSerializer의 StringEnumConverter).
// 서버 Java enum과 이름이 글자까지 같아야 한다. 이름을 바꾸거나 추가하면 서버 담당자와 같이 맞출 것.
// C→S: 클라이언트가 보냄, S→C: 서버가 보냄
public enum MessageType
{
    // ── 방 / 로비 ──
    CREATE_ROOM,        // C→S 방 만들기
    JOIN_ROOM,          // C→S 방 참가
    READY,              // C→S 준비 / 준비 해제
    LEAVE_ROOM,         // C→S 방 나가기
    ROOM_STATE,         // S→C 방 인원·준비 상태 갱신
    GAME_START,         // S→C 게임 시작 (GameSetup 값 전달)

    // ── 게임 진행 ──
    THROW_REQUEST,      // C→S 윷 던지기 요청
    RETHROW_REQUEST,    // C→S 재던지기 증강 사용 요청
    THROW_RESULT,       // S→C 서버가 정한 윷 결과 (던지기·재던지기 공통)
    MOVE,               // C→S 내가 둔 이동 + 이동 후 판단 정보(MoveReport)
    MOVE_APPLIED,       // S→C 확정된 이동 (상대 기기는 이걸로 재계산)
    TURN_CHANGED,       // S→C 턴 변경
    GAME_ENDED,         // S→C 게임 종료

    // ── 증강 ──
    AUGMENT_CHOICES,    // S→C 증강 후보
    SELECT_AUGMENT,     // C→S 증강 선택
    AUGMENT_SELECTED,   // S→C 증강 선택 확정 (상대에게도 알림)

    // ── 시스템 ──
    PING,               // C→S 연결 확인
    PONG,               // S→C 연결 확인 응답
    ERROR,              // S→C 요청 거부·오류
}
