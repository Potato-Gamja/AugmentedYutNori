using System.Threading.Tasks;

// 프로필 아이콘 뽑기 REST 호출 (로그인 토큰 필요). 받은 골드의 저장은 PlayerIdentity가 맡는다.
//   POST /gacha/draw  뽑기 1회 → GachaResultResponse (골드 부족 409 NOT_ENOUGH_GOLD)
// 보유 아이콘 조회는 도감, 프로필 아이콘 변경은 내 정보 담당
public class GachaApi
{
    private readonly ApiClient api;

    public GachaApi(ApiClient api)
    {
        this.api = api;
    }

    public async Task<GachaResultResponse> DrawAsync()
    {
        GachaResultResponse response = await api.Post<object, GachaResultResponse>("/gacha/draw", null);

        if (response == null || string.IsNullOrEmpty(response.iconId))
            throw new ApiException("POST", "/gacha/draw", 200, "응답에 iconId가 없습니다.", null);

        return response;
    }
}

// POST /gacha/draw 응답
public class GachaResultResponse
{
    public string iconId;
    public bool duplicate;  // 이미 가진 아이콘 (보상 없음)
    public int count;       // 이 아이콘을 뽑은 총 횟수
    public int gold;        // 차감 후 보유 골드
    public int price;
}
