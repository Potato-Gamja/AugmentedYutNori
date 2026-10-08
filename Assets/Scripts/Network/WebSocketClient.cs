using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

// NativeWebSocket 래퍼. 접속/해제, 메시지 송수신, PING/PONG을 맡는다.
// 받은 메시지는 백그라운드 스레드에서 도착하므로 큐에 쌓아 두고 Update(메인 스레드)에서 꺼내 이벤트로 넘긴다.
// 메시지 내용 해석(어떤 type이면 무엇을 할지)은 RemoteGameSession/로비 쪽에서 MessageReceived를 구독해 처리한다.
public class WebSocketClient : MonoBehaviour
{
    [SerializeField] private string url = "ws://localhost:8080/ws";

    // 로드밸런서 유휴 연결 제한(ALB 기본 60초)보다 짧게
    [SerializeField] private float pingInterval = 20f;

    private WebSocket socket;
    private float nextPingTime;
    private bool closingByUser;

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

    // 마지막 PING-PONG 왕복 시간(ms). 아직 없으면 -1
    public long LatencyMs { get; private set; } = -1;

    public event Action Connected;
    public event Action<bool> Disconnected;         // true = 내가 Close()로 끊음, false = 서버/네트워크 문제
    public event Action<string> Error;
    public event Action<WsEnvelope> MessageReceived;
    public event Action<long> LatencyUpdated;       // PONG을 받을 때마다 왕복 시간(ms)

    public string Url { get => url; set => url = value; }

    // guestToken이 있으면 쿼리로 붙인다 (WebGL은 헤더를 못 붙이므로 주소로 전달).
    // 서버 GuestTokenHandshakeInterceptor도 같은 guestToken 이름으로 읽는다.
    public async void Connect(string guestToken = null)
    {
        if (socket != null)
        {
            closingByUser = true; // 다시 접속하려고 기존 연결을 닫는 것
            await CloseSocket();
        }

        string target = string.IsNullOrEmpty(guestToken)
            ? url
            : $"{url}{(url.Contains("?") ? "&" : "?")}guestToken={Uri.EscapeDataString(guestToken)}";

        closingByUser = false;
        LatencyMs = -1;
        nextPingTime = 0f; // 붙자마자 한 번 재서 지연 시간을 바로 보여준다
        socket = new WebSocket(target);
        socket.OnOpen += OnOpen;
        socket.OnMessage += OnMessage;
        socket.OnError += OnError;
        socket.OnClose += OnClose;

        try
        {
            await socket.Connect(); // 연결이 끊길 때까지 기다리는 Task라 여기서 오래 머문다
        }
        catch (Exception exception)
        {
            OnError(exception.Message);
        }
    }

    public async void Close()
    {
        closingByUser = true;
        await CloseSocket();
    }

    // 내용이 있는 메시지
    public bool Send<T>(MessageType type, T payload)
    {
        return SendRaw(MessageSerializer.Serialize(type, payload));
    }

    // 내용이 없는 메시지 (THROW_REQUEST, LEAVE_ROOM 등)
    public bool Send(MessageType type)
    {
        return SendRaw(MessageSerializer.Serialize(type));
    }

    private bool SendRaw(string json)
    {
        if (!IsConnected)
        {
            Debug.LogWarning($"[WebSocket] 연결되지 않아 보내지 못했습니다: {json}", this);
            return false;
        }

        _ = socket.SendText(json);
        return true;
    }

    private void Update()
    {
        if (socket == null) return;

#if !UNITY_WEBGL || UNITY_EDITOR
        socket.DispatchMessageQueue(); // 쌓인 메시지를 메인 스레드에서 OnMessage로 전달
#endif

        if (IsConnected && Time.unscaledTime >= nextPingTime)
        {
            nextPingTime = Time.unscaledTime + pingInterval;
            Send(MessageType.PING, new PingMessage { clientTime = NowMs() });
        }
    }

    private void OnOpen()
    {
        Debug.Log($"[WebSocket] 연결됨: {url}", this);
        Connected?.Invoke();
    }

    private void OnMessage(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        WsEnvelope envelope;
        try
        {
            envelope = MessageSerializer.Deserialize(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[WebSocket] 메시지를 읽지 못했습니다: {exception.Message}\n{json}", this);
            return;
        }

        if (envelope == null) return;

        if (envelope.type == MessageType.PONG)
        {
            PongMessage pong = MessageSerializer.ReadPayload<PongMessage>(envelope);
            if (pong != null)
            {
                LatencyMs = Math.Max(0, NowMs() - pong.clientTime);
                LatencyUpdated?.Invoke(LatencyMs);
            }
        }

        MessageReceived?.Invoke(envelope);
    }

    private void OnError(string message)
    {
        Debug.LogWarning($"[WebSocket] 오류: {message}", this);
        Error?.Invoke(message);
    }

    private void OnClose(WebSocketCloseCode code)
    {
        Debug.Log($"[WebSocket] 연결 끊김: {code}", this);
        bool byUser = closingByUser;
        DetachSocket();
        Disconnected?.Invoke(byUser);
    }

    private async System.Threading.Tasks.Task CloseSocket()
    {
        if (socket == null) return;

        WebSocket closing = socket;
        if (closing.State == WebSocketState.Open || closing.State == WebSocketState.Connecting)
        {
            try { await closing.Close(); }
            catch (Exception exception) { Debug.LogWarning($"[WebSocket] 닫는 중 오류: {exception.Message}", this); }
        }

        // NativeWebSocket은 직접 닫을 때 OnClose를 안 부르거나 늦게 부를 수 있으므로 여기서 마무리한다
        if (socket == closing)
        {
            bool byUser = closingByUser;
            DetachSocket();
            Disconnected?.Invoke(byUser);
        }
    }

    private void DetachSocket()
    {
        if (socket == null) return;
        socket.OnOpen -= OnOpen;
        socket.OnMessage -= OnMessage;
        socket.OnError -= OnError;
        socket.OnClose -= OnClose;
        socket = null;
        LatencyMs = -1;
    }

    private async void OnDestroy()
    {
        closingByUser = true;
        await CloseSocket();
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
