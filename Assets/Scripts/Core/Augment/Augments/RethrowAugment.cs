// 게임당 1회, 방금 던진 윷 결과를 버리고 다시 던질 수 있게 해 주는 1회용 증강
public class RethrowAugment : Augment
{
    public override string Id => "Rethrow";

    // TODO: 카드 UI용. 이름/설명을 둘 곳(ScriptableObject 등)이 정해지면 옮긴다
    public string Name => "다시 던지기";
    public string Description => "방금 던진 윷 결과를 버리고 다시 던집니다.";

    public RethrowAugment(int ownerTeam) : base(ownerTeam) { }

    // 이동 규칙은 바꾸지 않으므로 이동 훅은 override하지 않는다.
    // "이번 턴에 말을 움직였는지"는 증강이 알 수 없으니 턴 흐름이 따로 확인한다
    public bool CanUse(BoardState state, int currentTeam, YutResult latestResult) =>
        currentTeam == OwnerTeam
        && !latestResult.GrantsExtraThrow()   // 윷/모에는 다시 던지기 불가
        && state.GetAugments(OwnerTeam).Contains(this);

    // 사용 허가와 소모만 한다. 실제로 다시 던지는 것은 호출한 쪽이 한다
    public bool TryUse(BoardState state, int currentTeam, YutResult latestResult)
    {
        if (!CanUse(state, currentTeam, latestResult)) return false;
        return state.RemoveAugment(this);
    }
}