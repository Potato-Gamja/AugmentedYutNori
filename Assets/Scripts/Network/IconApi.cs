using System.Collections.Generic;
using System.Threading.Tasks;

public class IconApi
{
    private readonly ApiClient api;

    public IconApi(ApiClient api)
    {
        this.api = api;
    }

    public async Task<PlayerProfileResponse> GetProfileAsync()
    {
        PlayerProfileResponse response = await api.Get<PlayerProfileResponse>("/players/me");
        if (response == null) throw new System.InvalidOperationException("프로필 응답이 비어 있습니다.");
        return response;
    }

    public Task ChangeIconAsync(string iconId)
    {
        return api.Patch<ChangeIconRequest, object>("/players/me/icon", new ChangeIconRequest { iconId = iconId });
    }

    public async Task<OwnedIconsResponse> GetOwnedAsync()
    {
        OwnedIconsResponse response = await api.Get<OwnedIconsResponse>("/players/me/icons");
        response ??= new OwnedIconsResponse();
        response.iconIds ??= new List<string>();
        return response;
    }
}

public class OwnedIconsResponse
{
    public List<string> iconIds = new List<string>();
}

public class PlayerProfileResponse
{
    public string nickname;
    public string iconId;
    public int gold;
}

public class ChangeIconRequest
{
    public string iconId;
}
