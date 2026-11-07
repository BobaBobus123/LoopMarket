using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TradeStorm.Api.Services;

public interface ICaptchaService
{
    (string Token, string Svg, string Question) GenerateCaptcha();
    bool ValidateCaptcha(string? token, string? answer);
}

public class CaptchaService : ICaptchaService
{
    private readonly byte[] _secretKey;

    public CaptchaService(IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? "LoopMarket_Secret_Master_Security_Key_2026_OWASP_Protection_Key";
        _secretKey = Encoding.UTF8.GetBytes(key);
    }

    private class CaptchaPayload
    {
        public int Answer { get; set; }
        public long Exp { get; set; }
        public string Salt { get; set; } = string.Empty;
    }

    public (string Token, string Svg, string Question) GenerateCaptcha()
    {
        var rand = new Random();
        int a = rand.Next(3, 19);
        int b = rand.Next(1, 10);
        int answer = a + b;
        long exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string salt = Guid.NewGuid().ToString("N").Substring(0, 8);

        var payload = new CaptchaPayload { Answer = answer, Exp = exp, Salt = salt };
        string json = JsonSerializer.Serialize(payload);
        string payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        using var hmac = new HMACSHA256(_secretKey);
        byte[] sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
        string sigB64 = Convert.ToBase64String(sigBytes);

        string token = $"{payloadB64}.{sigB64}";
        string question = $"{a} + {b} = ?";

        var svgBuilder = new StringBuilder();
        svgBuilder.Append("<svg xmlns='http://www.w3.org/2000/svg' width='140' height='42' viewBox='0 0 140 42' style='background:#f1f5f9; border-radius:10px; border:1.5px solid #cbd5e1; user-select:none;'>");
        for (int i = 0; i < 3; i++)
        {
            int y1 = rand.Next(5, 38);
            int y2 = rand.Next(5, 38);
            svgBuilder.Append($"<line x1='0' y1='{y1}' x2='140' y2='{y2}' stroke='#cbd5e1' stroke-width='1.5' />");
        }
        svgBuilder.Append($"<text x='20' y='28' font-family='Arial, sans-serif' font-weight='900' font-size='20' fill='#2563eb' letter-spacing='2'>{a} + {b} = ?</text>");
        svgBuilder.Append("</svg>");

        return (token, svgBuilder.ToString(), question);
    }

    public bool ValidateCaptcha(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer)) return false;
        if (!int.TryParse(answer.Trim(), out int parsedAnswer)) return false;

        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            string payloadB64 = parts[0];
            string sigB64 = parts[1];

            using var hmac = new HMACSHA256(_secretKey);
            byte[] expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
            if (Convert.ToBase64String(expectedSig) != sigB64) return false;

            string json = Encoding.UTF8.GetString(Convert.FromBase64String(payloadB64));
            var payload = JsonSerializer.Deserialize<CaptchaPayload>(json);
            if (payload == null) return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.Exp) return false;

            return payload.Answer == parsedAnswer;
        }
        catch
        {
            return false;
        }
    }
}
