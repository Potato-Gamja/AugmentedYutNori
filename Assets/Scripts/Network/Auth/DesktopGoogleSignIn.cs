#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// PC/에디터용 구글 로그인. 웹 빌드와 같은 Firebase JS SDK를 시스템 브라우저에서 돌린다.
// 1) localhost의 빈 포트에 작은 로그인 페이지를 띄우고 브라우저로 연다 (주소에 매번 바뀌는 무작위 경로를 넣는다)
// 2) 페이지에서 "Google로 로그인"을 누르면 Firebase JS SDK가 구글 팝업 로그인을 한다
// 3) 페이지가 Firebase ID 토큰을 같은 주소로 POST해서 이 게임에 돌려준다
// localhost는 Firebase 승인된 도메인에 기본으로 들어 있어 Google Cloud 콘솔 설정이 필요 없다.
// 비밀번호는 구글 팝업에만 입력되고 이 게임은 보지 못한다.
public class DesktopGoogleSignIn : IGoogleSignIn
{
    // FirebaseGoogleAuth.jslib와 같은 버전
    private const string FirebaseSdkVersion = "10.12.2";

    private readonly FirebaseAuthSettings settings;

    public bool StartOnPointerDown => false;

    public DesktopGoogleSignIn(FirebaseAuthSettings settings)
    {
        this.settings = settings;
    }

    public Task InitializeAsync()
    {
        if (settings == null) throw new GoogleSignInException("FirebaseAuthSettings가 연결되지 않았습니다.");
        if (string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.AuthDomain))
            throw new GoogleSignInException("FirebaseAuthSettings에 apiKey와 authDomain을 입력해 주세요.");

        return Task.CompletedTask;
    }

    public async Task<FirebaseSignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        string session = RandomUrlSafe(24);
        string baseUrl = $"http://localhost:{GetFreePort()}/{session}/";

        using (var listener = new HttpListener())
        {
            listener.Prefixes.Add(baseUrl);
            listener.Start();

            Application.OpenURL(baseUrl);
            return await Serve(listener, cancellationToken);
        }
    }

    // 로그인 페이지를 보여 주고, 페이지가 결과를 POST할 때까지 요청을 처리한다
    private async Task<FirebaseSignInResult> Serve(HttpListener listener, CancellationToken cancellationToken)
    {
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(settings.DesktopTimeoutSec), cancellationToken);

        while (true)
        {
            Task<HttpListenerContext> contextTask = listener.GetContextAsync();
            if (await Task.WhenAny(contextTask, timeoutTask) != contextTask)
                throw new GoogleSignInException(cancellationToken.IsCancellationRequested
                    ? "로그인을 취소했습니다. 다시 시도해 주세요."
                    : "로그인 시간이 초과되었습니다. 다시 시도해 주세요.", cancelled: true);

            HttpListenerContext context = contextTask.Result;
            string action = context.Request.Url.Segments.Length > 2 ? context.Request.Url.Segments[2].Trim('/') : "";

            if (context.Request.HttpMethod == "GET" && action == "")
            {
                Respond(context, 200, "text/html; charset=utf-8", BuildLoginPage());
            }
            else if (context.Request.HttpMethod == "POST" && action == "result")
            {
                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) body = reader.ReadToEnd();
                Respond(context, 200, "text/plain; charset=utf-8", "ok");

                var result = JsonConvert.DeserializeObject<PageResult>(body);
                if (string.IsNullOrEmpty(result?.idToken))
                    throw new GoogleSignInException("로그인 페이지 응답에 idToken이 없습니다.");

                return new FirebaseSignInResult
                {
                    IdToken = result.idToken,
                    RefreshToken = result.refreshToken,
                    Uid = result.uid,
                    Email = result.email,
                    DisplayName = result.displayName,
                };
            }
            else if (context.Request.HttpMethod == "POST" && action == "cancel")
            {
                Respond(context, 200, "text/plain; charset=utf-8", "ok");
                throw new GoogleSignInException("구글 로그인을 취소했습니다.", cancelled: true);
            }
            else
            {
                Respond(context, 404, "text/plain; charset=utf-8", "not found");
            }
        }
    }

    private string BuildLoginPage()
    {
        // </script> 같은 문자열이 페이지를 깨지 않도록 HTML 이스케이프해서 넣는다
        string config = JsonConvert.SerializeObject(new WebConfig
        {
            apiKey = settings.ApiKey,
            authDomain = settings.AuthDomain,
            projectId = settings.ProjectId,
            appId = settings.AppId,
        }, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml });

        string sdk = $"https://www.gstatic.com/firebasejs/{FirebaseSdkVersion}";
        return $@"<!doctype html>
<html lang=""ko""><head><meta charset=""utf-8""><title>증강 윷놀이 로그인</title>
<style>
  body {{ font-family: sans-serif; background: #304c7a; color: #3a2418; margin: 0; }}
  .card {{ background: #fff8ec; max-width: 420px; margin: 120px auto; padding: 40px; text-align: center; border-radius: 12px; }}
  button {{ font-size: 18px; padding: 14px 28px; border: 0; border-radius: 8px; cursor: pointer; }}
  button#login {{ background: #d75f50; color: #fff; width: 100%; }}
  button#cancel {{ background: transparent; color: #7a6a60; margin-top: 12px; font-size: 14px; }}
  div#status {{ min-height: 24px; margin-top: 16px; }}
</style></head>
<body><div class=""card"">
  <h2>증강 윷놀이</h2>
  <p>구글 계정으로 로그인하세요</p>
  <button id=""login"" disabled>Google로 로그인</button><br>
  <button id=""cancel"">취소</button>
  <div id=""status"">준비 중...</div>
</div>
<script src=""{sdk}/firebase-app-compat.js""></script>
<script src=""{sdk}/firebase-auth-compat.js""></script>
<script>
  var loginButton = document.getElementById('login');
  var cancelButton = document.getElementById('cancel');
  var statusText = document.getElementById('status');
  function finish(message) {{
    loginButton.style.display = 'none';
    cancelButton.style.display = 'none';
    statusText.textContent = message;
  }}
  try {{
    firebase.initializeApp({config});
    loginButton.disabled = false;
    statusText.textContent = '';
  }} catch (e) {{
    statusText.textContent = 'Firebase 초기화 실패: ' + e.message;
  }}
  loginButton.onclick = function () {{
    var provider = new firebase.auth.GoogleAuthProvider();
    provider.setCustomParameters({{ prompt: 'select_account' }});
    loginButton.disabled = true;
    statusText.textContent = '구글 로그인 창에서 계정을 선택해 주세요.';
    firebase.auth().signInWithPopup(provider)
      .then(function (credential) {{
        var user = credential.user;
        return user.getIdToken().then(function (idToken) {{
          return fetch('result', {{
            method: 'POST',
            body: JSON.stringify({{ idToken: idToken, refreshToken: user.refreshToken, uid: user.uid, email: user.email, displayName: user.displayName }})
          }});
        }});
      }})
      .then(function () {{ finish('로그인되었습니다. 이 창을 닫고 게임으로 돌아가세요.'); }})
      .catch(function (e) {{
        loginButton.disabled = false;
        statusText.textContent = e.code === 'auth/popup-closed-by-user'
          ? '로그인 창이 닫혔습니다. 다시 눌러 주세요.'
          : '로그인 실패: ' + (e.message || e);
      }});
  }};
  cancelButton.onclick = function () {{
    fetch('cancel', {{ method: 'POST' }}).finally(function () {{ finish('취소했습니다. 이 창을 닫아 주세요.'); }});
  }};
</script></body></html>";
    }

    private static void Respond(HttpListenerContext context, int status, string contentType, string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = body.Length;
        context.Response.OutputStream.Write(body, 0, body.Length);
        context.Response.OutputStream.Close();
    }

    private static int GetFreePort()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        int port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private static string RandomUrlSafe(int byteCount)
    {
        var bytes = new byte[byteCount];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private class WebConfig
    {
        public string apiKey;
        public string authDomain;
        public string projectId;
        public string appId;
    }

    // 로그인 페이지가 POST하는 내용
    private class PageResult
    {
        public string idToken;
        public string refreshToken;
        public string uid;
        public string email;
        public string displayName;
    }
}
#endif
