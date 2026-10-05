using Newtonsoft.Json.Linq;

// 모든 WebSocket 메시지의 공통 겉모양.
// { "type": "THROW_RESULT", "seq": 12, "payload": { ... } }
// 받는 쪽은 type을 보고 payload를 어떤 메시지 클래스로 읽을지 정한다 (MessageSerializer.ReadPayload).
public class WsEnvelope
{
    public MessageType type;

    // 서버가 보낸 메시지의 순번. 1부터 증가하며, 빠지거나 순서가 바뀌었는지 확인할 때 쓴다.
    // 클라이언트가 보낼 때는 0으로 둔다.
    public long seq;

    // 실제 내용. 아직 어떤 클래스인지 모르므로 JSON 객체 그대로 들고 있는다.
    public JObject payload;

    public WsEnvelope() { }

    public WsEnvelope(MessageType type, JObject payload, long seq = 0)
    {
        this.type = type;
        this.payload = payload ?? new JObject();
        this.seq = seq;
    }

    public override string ToString() => $"[{seq}] {type}";
}
