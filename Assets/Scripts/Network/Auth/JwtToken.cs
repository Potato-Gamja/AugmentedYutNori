using System;
using System.Text;
using Newtonsoft.Json.Linq;

// 서버 로그인 토큰(JWT)의 만료 시각만 읽는다. 서명 검증은 서버가 하므로 클라는 내용만 본다.
// JWT = header.payload.signature (각 부분은 base64url). payload의 exp는 만료 시각(초 단위 Unix 시간)
public static class JwtToken
{
    public static bool TryGetExpiry(string token, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        if (string.IsNullOrEmpty(token)) return false;

        string[] parts = token.Split('.');
        if (parts.Length != 3) return false;   // 예전 UUID 토큰 등 JWT가 아닌 값

        try
        {
            JObject payload = JObject.Parse(Encoding.UTF8.GetString(DecodeBase64Url(parts[1])));
            long? exp = payload.Value<long?>("exp");
            if (exp == null) return false;

            expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.Value);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 만료가 margin 안으로 다가왔는지. JWT가 아니라 만료를 모르면 false (서버가 401로 알려 주면 그때 처리)
    public static bool IsExpiringWithin(string token, TimeSpan margin)
    {
        return TryGetExpiry(token, out DateTimeOffset expiresAt) &&
               expiresAt - DateTimeOffset.UtcNow <= margin;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
