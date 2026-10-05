using System;
using System.Collections.Generic;

// 모든 증강을 등록해 두고, 팀별 후보를 뽑거나(로컬) 받아서(서버) 저장하고, 후보 안에서만 선택을 허용하는 풀
public class AugmentPool
{
    private readonly Dictionary<string, Func<int, IAugment>> factories = new();
    private readonly List<string> registeredIds = new();                            // 등록 순서 (json 순서). 후보 순서를 고정하는 데 사용
    private readonly Dictionary<int, HashSet<string>> picked = new();               // 팀 → 이미 뽑은 증강 Id
    private readonly Dictionary<int, List<string>> offered = new();                 // 팀 → 지금 제시된 후보 Id
    private readonly IRandomProvider random;                                        // 로컬 후보 뽑기용. 서버 후보에는 쓰지 않는다

    // 로컬 모드는 SystemRandomProvider, 테스트는 SequenceRandomProvider를 넣는다
    public AugmentPool(IRandomProvider random)
    {
        if (random == null) 
            throw new ArgumentNullException(nameof(random));
        
        this.random = random;
    }

    // min 이상 max 미만의 정수. NextDouble()이 1.0을 넘지 않으므로 max는 나오지 않는다
    private int NextIndex(int min, int max)
    {
        return min + (int)(random.NextDouble() * (max - min));
    }

    // 예: pool.Register("Rethrow", team => new RethrowAugment(team));
    public void Register(string id, Func<int, IAugment> factory)
    {
        factories.Add(id, factory);
        registeredIds.Add(id);
    }

    // 로컬 모드: 이 팀이 아직 안 뽑은 증강 중에서 count개를 중복 없이 고르고, 그 결과를 이 팀의 후보로 저장한다
    public List<string> GetRandomIds(int team, int count)
    {
        HashSet<string> teamPicked = GetPicked(team);
        var candidates = new List<string>();
        foreach (string id in registeredIds)
        {
            if (!teamPicked.Contains(id)) candidates.Add(id);
        }

        count = Math.Min(count, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int j = NextIndex(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var result = candidates.GetRange(0, count);
        offered[team] = new List<string>(result);
        return result;
    }

    // 멀티: 서버가 정한 후보를 검사한 뒤 이 팀의 후보로 저장한다. 랜덤은 쓰지 않는다
    public void SetOffered(int team, IReadOnlyList<string> ids)
    {
        if (ids == null) 
            throw new ArgumentNullException(nameof(ids));
        HashSet<string> teamPicked = GetPicked(team);
        HashSet<string> seen = new HashSet<string>();
        foreach (string id in ids)
        {
            if (!factories.ContainsKey(id))
                throw new ArgumentException($"'{id}'는 등록되지 않은 증강입니다. augments.json과 AugmentFactories를 확인하세요.");
            if (teamPicked.Contains(id))
                throw new ArgumentException($"'{id}'는 팀 {team}이 이미 선택한 증강입니다.");
            if (!seen.Add(id))
                throw new ArgumentException($"증강 후보에 '{id}'가 두 번 있습니다.");
        }
        offered[team] = new List<string>(ids);
    }

    // 카드를 골랐을 때 호출. 이 팀의 후보 안에 있는지 확인하고, 기록을 남기고, 후보를 비운 뒤 그 팀 소유의 새 증강을 만든다
    public IAugment Pick(int team, string id)
    {
        if (!offered.ContainsKey(team))
            throw new InvalidOperationException($"팀 {team}에게 제시된 증강 후보가 없습니다. Pick 전에 GetRandomIds 또는 SetOffered를 호출하세요.");
        if (!offered[team].Contains(id))
            throw new ArgumentException($"'{id}'는 팀 {team}에게 제시된 증강 후보가 아닙니다.");
        GetPicked(team).Add(id);
        offered.Remove(team);
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
// 로컬
// List<string> choices = pool.GetRandomIds(team, 3);     // 카드 3장 표시 (후보로 저장됨)
// IAugment augment = pool.Pick(team, choices[selected]); // 후보 안에서만 고를 수 있음
// 멀티
// pool.SetOffered(team, serverIds);                      // 서버가 보낸 후보 저장 (랜덤 사용 안 함)
// IAugment augment = pool.Pick(team, selectedId);
// state.AddAugment(augment);