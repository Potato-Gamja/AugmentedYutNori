using UnityEngine;

public interface IRandomProvider
{
    double NextDouble();   // 0.0 이상 1.0 미만
}

// 실제 게임용
public class SystemRandomProvider : IRandomProvider
{
    private readonly System.Random random;

    public SystemRandomProvider() => random = new System.Random();
    public SystemRandomProvider(int seed) => random = new System.Random(seed); // 윷이나 모 강제?

    public double NextDouble() => random.NextDouble();
}

// 테스트용: 넣어둔 값을 차례로 반환 var random = new SequenceRandomProvider(0.1, 0.5, 0.9); 같은 것으로 사용
public class SequenceRandomProvider : IRandomProvider
{
    private readonly double[] values;
    private int index;

    public SequenceRandomProvider(params double[] values)
    {
        if (values == null || values.Length == 0)
            throw new System.ArgumentException("값을 최소 1개 넣어야 합니다.");
        this.values = values;
    }

    public double NextDouble()
    {
        double v = values[index % values.Length];  // 끝까지 쓰면 처음부터 다시
        index++;
        return v;
    }
}