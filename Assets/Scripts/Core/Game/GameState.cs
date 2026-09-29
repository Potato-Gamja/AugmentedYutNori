using System.Collections.Generic;

public class GameState
{
    // 현재 보드의 상태를 연결
    public BoardState Board { get; }
    // 방금 던진 윷 결과 기억
    public List<YutResult> StoredResults { get; } = new List<YutResult>();

    // 플레이어가 앞으로 윷을 던질 수 있는 횟수
    // 윷이나 모가 나오면 추가 기회는 result.GrantsExtraThrow에
    public int RemainingThrows { get; set; }
    // 승자가 없으면 null, 승자가 나오면 팀 번호 저장
    public int? WinnerTeam { get; set; }
    // 이번 턴에 말을 한 번이라도 움직였는지
    public bool HasMovedThisTurn { get; set; }

    public GameState(BoardState board)  
    {
        Board = board;
    }

    // 가장 최근에 던진 결과
    public YutResult? LatestYutResult
    {
        get
        {
            if (StoredResults.Count == 0)
                return null;
            return StoredResults[StoredResults.Count - 1];
        }
    }
}
