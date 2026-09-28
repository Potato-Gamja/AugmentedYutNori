using UnityEngine;

public readonly struct YutThrowOutcome
{
    public readonly bool[] Sticks;     // true = 평평한 면(배)
    public readonly YutResult Result;

    public YutThrowOutcome(bool[] sticks, YutResult result)
    {
        Sticks = sticks;
        Result = result;
    }
}
public interface IGetResult //차후 윷가락이 늘어날지도 모르기 때문에 메서드 분리
{
    YutResult GetResult(bool[] sticks);
}

//var rule = new StandardGetResult(); 백도 끄려면 useBackDo: false
public class StandardGetResult : IGetResult // 윷이 4개일 경우 사용할 로직
{
    public const int StickCount = 4;
    private const int MarkedStick = 0;   // 표시된 윷가락 1번째 윷
    private readonly bool useBackDo;

    public StandardGetResult(bool useBackDo = true)
    {
        this.useBackDo = useBackDo;
    }

    public YutResult GetResult(bool[] sticks)
    {
        if (sticks.Length != StickCount)
            throw new System.ArgumentException($"기본 규칙은 윷가락 {StickCount}개만 판정합니다. 받은 개수: {sticks.Length}");

        int flatCount = 0;
        foreach (bool isFlat in sticks)
        {
            if (isFlat) flatCount++;
        }

        switch (flatCount)
        {
            case 0: return YutResult.Mo;
            case 1:
                if (useBackDo && sticks[MarkedStick]) return YutResult.BackDo;
                return YutResult.Do;
            case 2: return YutResult.Gae;
            case 3: return YutResult.Geol;
            case 4: return YutResult.Yut;
            default:
                throw new System.InvalidOperationException($"판정할 수 없는 평평한 면 개수: {flatCount}");
        }
    }
}

// var thrower = new YutThrower(new SystemRandomProvider()); 형식으로 호출
// YutThrowOutcome outcome = thrower.Throw(StandardGetResult.StickCount, rule);
public class YutThrower
{
    private readonly IRandomProvider random;
    private readonly double flatChance;

    public YutThrower(IRandomProvider random, double flatChance = 0.5)
    {
        this.random = random;
        this.flatChance = flatChance;
    }

    public YutThrowOutcome Throw(int stickCount, IGetResult getResult)
    {
        bool[] sticks = new bool[stickCount];
        for (int i = 0; i < stickCount; i++)
            sticks[i] = random.NextDouble() < flatChance;

        return new YutThrowOutcome(sticks, getResult.GetResult(sticks));
    }
}