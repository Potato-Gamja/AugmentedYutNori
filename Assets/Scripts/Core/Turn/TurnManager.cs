using UnityEngine;

public class TurnManager
{
    private readonly GameState state;

    // 현재 누구의 턴인지 저장 
    public int CurrentTeam { get; private set; }
    public int TeamCount { get; }
    public int Round { get; private set; }

    public TurnManager(GameState state, int teamCount)
    {
        this.state = state;
        TeamCount = teamCount;
        CurrentTeam = 0;
        Round = 1;
        StartTurn();
    }

    // 새로운 턴이 시작되면 던질 횟수 1, 윷 결과 없음으로 초기화
    public void StartTurn()
    {
        state.RemainingThrows = 1;
        state.CurrentYutResult = null;
        state.HasMovedThisTurn = false;
    }

    // YutResult가 도라면 1-1 해서 RemainingThrows = 0
    // YutResult가 윷이라면 1-1 하고, 다시 +1을 해서 RemainingThrows = 1
    public void ConsumeThrow(YutResult result)
    {
        state.RemainingThrows--;
        if (result.GrantsExtraThrow())
        {
            state.RemainingThrows++;
        }
    }

    // 윷/모 이외의 혹시 모를 규칙 때문에 추가 기회 받는 경우
    // 예를 들어서 상대 말을 잡는 경우
    public void AddExtraThrow()
    {
        state.RemainingThrows++;
    }

    public bool HasRemainingThrow()
    {
        return state.RemainingThrows > 0;
    }

    public void NextTurn()
    {
        CurrentTeam++;

        // 한바퀴 돌면 다시 첫번째 팀의 턴으로
        if (CurrentTeam >= TeamCount)
        {
            CurrentTeam = 0;
            Round++;
        }
        StartTurn();
    }
}
