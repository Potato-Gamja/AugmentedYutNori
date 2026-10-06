using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

// REST 호출 공통 처리. 서버 주소, JSON 변환(WebSocket과 같은 MessageSerializer 규칙), 인증 헤더를 한 곳에서 맡는다.
// 예) var res = await api.Post<GuestLoginRequest, GuestLoginResponse>("/players", new GuestLoginRequest { nickname = "윷돌이" });
public class ApiClient
{
    public string BaseUrl { get; set; }

    // 있으면 Authorization: Bearer {Token}으로 붙인다 (PlayerIdentity가 넣어 준다)
    public string Token { get; set; }

    public int TimeoutSec { get; set; } = 10;

    public ApiClient(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
    }

    public Task<TResponse> Get<TResponse>(string path)
    {
        return Send<TResponse>(UnityWebRequest.kHttpVerbGET, path, null);
    }

    public Task<TResponse> Post<TRequest, TResponse>(string path, TRequest body)
    {
        return Send<TResponse>(UnityWebRequest.kHttpVerbPOST, path, JsonConvert.SerializeObject(body, MessageSerializer.Settings));
    }

    private async Task<TResponse> Send<TResponse>(string method, string path, string json)
    {
        using var request = new UnityWebRequest(BaseUrl + path, method)
        {
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = TimeoutSec,
        };

        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }
        if (!string.IsNullOrEmpty(Token)) request.SetRequestHeader("Authorization", $"Bearer {Token}");

        await SendAsync(request);

        string text = request.downloadHandler.text;
        if (request.result != UnityWebRequest.Result.Success)
            throw new ApiException(method, path, request.responseCode, request.error, text);

        return string.IsNullOrEmpty(text) ? default : JsonConvert.DeserializeObject<TResponse>(text, MessageSerializer.Settings);
    }

    // UnityWebRequest는 메인 스레드에서 끝나므로 이어지는 코드도 메인 스레드에서 실행된다
    private static Task SendAsync(UnityWebRequest request)
    {
        var done = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => done.TrySetResult(true);
        return done.Task;
    }
}

// REST 호출 실패 (네트워크 오류 또는 2xx가 아닌 응답)
public class ApiException : Exception
{
    public long StatusCode { get; }   // 0이면 서버에 닿지 못함 (주소 오류, 서버 꺼짐, 시간 초과)
    public string ResponseBody { get; }

    public ApiException(string method, string path, long statusCode, string error, string responseBody)
        : base($"{method} {path} 실패 ({statusCode}): {error}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
