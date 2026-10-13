using System.Collections.Generic;
using System.Threading.Tasks;

// 업적 REST 호출 (로그인 토큰 필요). 받은 골드의 저장은 PlayerIdentity가 맡는다.
//   GET  /players/me/achievements             내 업적 목록 (이름·설명·진행도·달성·수령 여부까지 서버가 준다)
//   POST /players/me/achievements/{id}/claim  보상 받기 (달성 전 409 ACHIEVEMENT_NOT_ACHIEVED, 이미 받음 409 ACHIEVEMENT_ALREADY_CLAIMED)
public class AchievementApi
{
    private readonly ApiClient api;

    public AchievementApi(ApiClient api)
    {
        this.api = api;
    }

    public async Task<AchievementListResponse> GetAsync()
    {
        AchievementListResponse response = await api.Get<AchievementListResponse>("/players/me/achievements");
        response ??= new AchievementListResponse();
        response.achievements ??= new List<AchievementEntry>();
        return response;
    }

    public async Task<ClaimAchievementResponse> ClaimAsync(string achievementId)
    {
        string path = $"/players/me/achievements/{UnityEngine.Networking.UnityWebRequest.EscapeURL(achievementId)}/claim";
        ClaimAchievementResponse response = await api.Post<object, ClaimAchievementResponse>(path, null);

        if (response == null)
            throw new ApiException("POST", path, 200, "응답이 비어 있습니다.", null);

        return response;
    }
}

// GET /players/me/achievements 응답
public class AchievementListResponse
{
    public List<AchievementEntry> achievements = new List<AchievementEntry>();
}

// 업적 한 줄
public class AchievementEntry
{
    public string id;
    public string name;
    public string description;
    public string stat;         // LOGIN_DAYS, GAMES_PLAYED, GACHA_DRAWS, FIRST_PLACES
    public int goal;
    public int progress;        // goal을 넘으면 goal
    public int rewardGold;
    public bool achieved;       // 달성 → 보상 받기 가능
    public bool claimed;        // 보상 이미 받음
}

// POST /players/me/achievements/{id}/claim 응답
public class ClaimAchievementResponse
{
    public string achievementId;
    public int rewardGold;      // 이번에 받은 골드
    public int gold;            // 받은 뒤 보유 골드
}
