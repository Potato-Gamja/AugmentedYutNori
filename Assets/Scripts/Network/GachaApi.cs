using System.Collections.Generic;
using System.Threading.Tasks;

// 프로필 아이콘 뽑기 REST 호출 (로그인 토큰 필요). 받은 골드의 저장은 PlayerIdentity가 맡는다.
//   POST /gacha/draw?count=N  뽑기 N회(1 또는 10) → GachaResultResponse (골드 부족 409 NOT_ENOUGH_GOLD)
// 보유 아이콘 조회는 도감, 프로필 아이콘 변경은 내 정보 담당
public class GachaApi
{
    public const int TenDrawCount = 10;

    private readonly ApiClient api;

    public GachaApi(ApiClient api)
    {
        this.api = api;
    }

    public async Task<GachaResultResponse> DrawAsync(int count = 1)
    {
        string path = count == 1 ? "/gacha/draw" : $"/gacha/draw?count={count}";
        GachaResultResponse response = await api.Post<object, GachaResultResponse>(path, null);

        if (response == null || string.IsNullOrEmpty(response.iconId))
            throw new ApiException("POST", path, 200, "응답에 iconId가 없습니다.", null);

        // results가 없는 예전 서버는 1회 결과만 준다
        if (response.results == null || response.results.Count == 0)
        {
            response.results = new List<GachaDrawResult>
            {
                new GachaDrawResult { iconId = response.iconId, duplicate = response.duplicate, count = response.count },
            };
        }

        return response;
    }
}

// POST /gacha/draw 응답
public class GachaResultResponse
{
    public List<GachaDrawResult> results;  // 뽑은 순서대로 (1회면 1개, 10연차면 10개)
    public int gold;        // 차감 후 보유 골드
    public int price;       // 이번에 쓴 골드 (가격 × 횟수)

    // 첫 번째 결과와 같다 (1회 뽑기 호환용)
    public string iconId;
    public bool duplicate;
    public int count;
}

// 뽑기 한 번의 결과
public class GachaDrawResult
{
    public string iconId;
    public bool duplicate;  // 이미 가진 아이콘 (보상 없음). 같은 10연차 안에서 두 번째로 나온 것도 true
    public int count;       // 이 결과까지 반영한 보유 횟수
}
