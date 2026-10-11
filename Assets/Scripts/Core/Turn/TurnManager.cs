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
        state.HasRethrownThisTurn = false;
    }

    // 윷을 한 번 던졌을 때 호출
    // grantsExtraThrow: 이번 결과로 한 번 더 던지는지 (GameRules가 증강까지 보고 정함)
    public void ConsumeThrow(bool grantsExtraThrow)
    {
        if (state.RemainingThrows <= 0)
            return;
        state.RemainingThrows--;

        if (grantsExtraThrow)
        {
            state.RemainingThrows++;    
        }
    }

    // 윷/모 이외의 혹시 모를 규칙 때문에 추가 기회 받는 경우
    // 예를 들어서 상대 말을 잡는 경우
    public void AddExtraThrow(int count = 1)
    {
        state.RemainingThrows += count;
    }

    public bool HasRemainingThrow()
    {
        return state.RemainingThrows > 0;
    }

    // 재던지기로 기존 결과가 새 결과로 교체됐을 때
    // 기본 던지기 횟수는 다시 소비하지 않고, 윷/모 추가 기회만 다시 계산한다.
    public void ReplaceThrowResult(
        bool oldGrantsExtraThrow,
        bool newGrantsExtraThrow)
    {
        if (oldGrantsExtraThrow)
        {
            state.RemainingThrows--;
        }

        if (newGrantsExtraThrow)
        {
            state.RemainingThrows++;
        }
    }

    // 서버가 전달한 턴 상태 적용
    public void ApplyServerTurn(int team, int remainingThrows)
    {
        if (team < 0 || team >= TeamCount)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(team),
                $"팀 번호가 올바르지 않습니다: {team}");
        }

        if (remainingThrows < 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(remainingThrows),
                "남은 던지기 횟수는 0보다 작을 수 없습니다.");
        }

        // 서버에서 확정한 값으로 현재 턴과 남은 던지기 횟수를 덮어쓴다.
        CurrentTeam = team;
        state.RemainingThrows = remainingThrows;

        // 새로운 턴이므로 이전 턴에서 사용하던 상태 초기화
        state.StoredResults.Clear();
        state.HasMovedThisTurn = false;
        state.HasRethrownThisTurn = false;
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
