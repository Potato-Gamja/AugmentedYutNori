using System.Collections.Generic;
using System.Threading.Tasks;

public class IconApi
{
    private readonly ApiClient api;

    public IconApi(ApiClient api)
    {
        this.api = api;
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
