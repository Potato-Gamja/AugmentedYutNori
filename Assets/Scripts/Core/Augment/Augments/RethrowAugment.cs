// 매 턴 첫 굴리기에서 한 번, 방금 던진 결과를 버리고 다시 던질 수 있게 해 주는 증강
public class RethrowAugment : Augment
{
    public override string Id => "Rethrow";

    public RethrowAugment(int ownerTeam) : base(ownerTeam) { }

    // 이동 규칙은 바꾸지 않으므로 이동 훅은 override하지 않는다.
    public bool CanUse(GameState state, int currentTeam)
    {
        if (currentTeam != OwnerTeam) return false;                                 // 내 차례
        if (!state.Board.GetAugments(OwnerTeam).Contains(this)) return false;       // 보유 여부
        if (state.HasMovedThisTurn) return false;                                   // 이동 전
        if (state.HasRethrownThisTurn) return false;                                // 턴당 1회
        if (state.StoredResults.Count != 1) return false;                           // 턴의 첫 굴리기

        YutResult? latest = state.LatestYutResult;
        return latest.HasValue && !latest.Value.GrantsExtraThrow();                 // 윷·모 불가
    }
}