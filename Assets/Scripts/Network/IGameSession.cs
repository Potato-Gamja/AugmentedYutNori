using System;
using System.Collections.Generic;

// 입력(요청)과 실제 규칙 실행 사이의 창구.
// GameFlowController는 이 인터페이스로 요청만 보내고, 결과는 이벤트로 받는다.
// 로컬 플레이는 LocalGameSession(바로 실행), 서버 연결은 RemoteGameSession(서버 확인 후 이벤트)으로 바꿔 끼운다.
public interface IGameSession
{
    // 요청: 결과를 바로 돌려주지 않는다. 처리되면 아래 결과 이벤트가, 거절되면 RequestRejected가 온다
    void RequestThrow();
    void RequestMove(int pieceId, int resultIndex, int destinationNodeId);
    void RequestAugment(string augmentId);
    void RequestRethrow();

    // 요청을 보내고 결과를 기다리는 중인지. true인 동안 HUD 입력을 잠근다 (로컬은 바로 처리되므로 항상 false)
    bool IsCommandPending { get; }

    // 결과: 확정된 것만 알린다
    event Action<YutThrowOutcome> Thrown;
    event Action<MoveResult> Moved;
    event Action<int> TurnChanged;                              // 새 턴의 팀
    event Action<int, IReadOnlyList<string>> AugmentChoices;    // 증강을 고를 팀, 후보 증강 id
    event Action<int, IAugment> AugmentSelected;                // 고른 팀, 고른 증강
    event Action<int> GameEnded;                                // 승리 팀

    // 요청이 받아들여지지 않았을 때 (규칙 위반, 서버 거절 등). 사유 문자열
    event Action<string> RequestRejected;
}
