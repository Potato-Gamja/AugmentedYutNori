using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

// 메시지 ↔ JSON 문자열 변환을 한 곳에서 처리한다.
// 규칙 (서버 Jackson 설정과 동일해야 함)
//  - 필드 이름: camelCase (pieceId, stateHash)
//  - enum: 숫자가 아니라 이름 문자열 ("THROW_RESULT", "Gae")
//  - null 필드는 보내지 않음
public static class MessageSerializer
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() },   // 이름 그대로 (camelCase 변환 안 함)
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,   // 서버가 필드를 추가해도 깨지지 않게
    };

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

    // 보낼 때: 메시지 종류 + 내용 → JSON 문자열
    public static string Serialize<T>(MessageType type, T payload, long seq = 0)
    {
        JObject body = payload == null ? new JObject() : JObject.FromObject(payload, Serializer);
        return JsonConvert.SerializeObject(new WsEnvelope(type, body, seq), Settings);
    }

    // 내용이 없는 메시지 (THROW_REQUEST, LEAVE_ROOM 등)
    public static string Serialize(MessageType type, long seq = 0)
    {
        return JsonConvert.SerializeObject(new WsEnvelope(type, new JObject(), seq), Settings);
    }

    // 받을 때 1단계: JSON 문자열 → 봉투 (type 확인용)
    public static WsEnvelope Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<WsEnvelope>(json, Settings);
    }

    // 받을 때 2단계: type을 확인한 뒤 payload를 원하는 메시지 클래스로 변환
    // 예) if (env.type == MessageType.THROW_RESULT) var msg = MessageSerializer.ReadPayload<ThrowResultMessage>(env);
    public static T ReadPayload<T>(WsEnvelope envelope)
    {
        if (envelope?.payload == null) return default;
        return envelope.payload.ToObject<T>(Serializer);
    }
}
