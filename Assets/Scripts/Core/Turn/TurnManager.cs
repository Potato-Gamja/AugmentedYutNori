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
        state.StoredResults.Clear();
        state.HasMovedThisTurn = false;
    }

    // 윷을 한 번 던졌을 때 호출
    public void ConsumeThrow(YutResult result)
    {
        if (state.RemainingThrows <= 0)
            return;
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

    // 재던지기로 기존 결과가 새 결과로 교체됐을 때
    // 기본 던지기 횟수는 다시 소비하지 않고, 윷/모 추가 기회만 다시 계산한다.
    public void ReplaceThrowResult(
        YutResult oldResult,
        YutResult newResult)
    {
        if (oldResult.GrantsExtraThrow())
        {
            state.RemainingThrows--;
        }

        if (newResult.GrantsExtraThrow())
        {
            state.RemainingThrows++;
        }
    }

    public void NextTurn()
    {
        CurrentTeam++;
        if (CurrentTeam >= TeamCount)
        {
            CurrentTeam = 0;
            Round++;
        }
        StartTurn();
    }
}
