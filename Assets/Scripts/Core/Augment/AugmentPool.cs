using System;
using System.Collections.Generic;

// 모든 증강을 등록해 두고, 팀별로 아직 안 뽑은 증강 중에서 선택지를 뽑아 주는 풀
public class AugmentPool
{
    private readonly Dictionary<string, Func<int, IAugment>> factories = new();
    private readonly Dictionary<int, HashSet<string>> picked = new();   // 팀 → 이미 뽑은 증강 Id
    private readonly Random random = new();

    // 예: pool.Register("Rethrow", team => new RethrowAugment(team));
    public void Register(string id, Func<int, IAugment> factory)
    {
        factories.Add(id, factory);
    }

    // 이 팀이 아직 안 뽑은 증강 중에서 count개를 중복 없이 고른다
    public List<string> GetRandomIds(int team, int count)
    {
        HashSet<string> teamPicked = GetPicked(team);
        var candidates = new List<string>();
        foreach (string id in factories.Keys)
        {
            if (!teamPicked.Contains(id)) candidates.Add(id);
        }

        count = Math.Min(count, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int j = random.Next(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        return candidates.GetRange(0, count);
    }


    // 카드를 골랐을 때 호출. 기록을 남기고 그 팀 소유의 새 증강을 만든다
    public IAugment Pick(int team, string id)
    {
        GetPicked(team).Add(id);
        return factories[id](team);
    }

    private HashSet<string> GetPicked(int team)
    {
        if (!picked.TryGetValue(team, out HashSet<string> set))
        {
            set = new HashSet<string>();
            picked.Add(team, set);
        }
        return set;
    }
}

// 사용 예시
// List<string> choices = pool.GetRandomIds(team, 3);    // 카드 3장 표시
// IAugment augment = pool.Pick(team, choices[selected]); // 고른 카드
// state.AddAugment(augment);