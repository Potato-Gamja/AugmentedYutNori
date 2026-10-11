// 잡으면 추가 던지기 2번, 대신 윷·모가 나와도 한 번 더 던지지 못하는 증강
public class HunterAugment : Augment
{
    public override string Id => "Hunter";

    // 잡았을 때 받는 추가 던지기 수 (기본 1 + 사냥꾼 1)
    public const int CaptureBonusThrows = 2;

    public HunterAugment(int ownerTeam) : base(ownerTeam) { }

    // 이동 규칙은 바꾸지 않으므로 이동 훅은 override하지 않는다.
    // 추가 던지기 규칙은 GameRules(HasHunter·GrantsExtraThrow)가 이 증강을 가졌는지 보고 적용한다
}
