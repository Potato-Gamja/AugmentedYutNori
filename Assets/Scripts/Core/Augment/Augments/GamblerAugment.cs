// 던진 결과가 항상 도↔모로 뒤바뀌는 증강 (바뀐 결과는 추가 던지기 없음)
public class GamblerAugment : Augment
{
    public override string Id => "Gambler";

    public GamblerAugment(int ownerTeam) : base(ownerTeam) { }

    // 도는 모로, 모는 도로 바꾼다. 나머지는 그대로
    public static YutResult Swap(YutResult result)
    {
        return result switch
        {
            YutResult.Do => YutResult.Mo,
            YutResult.Mo => YutResult.Do,
            _ => result
        };
    }

    // 이동 규칙은 바꾸지 않으므로 이동 훅은 override하지 않는다.
    // 결과 변환은 GameRules.ApplyThrow가 이 증강을 가졌는지 보고 적용한다
}
