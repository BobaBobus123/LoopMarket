using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TradeStorm.Api.Services;

public record EmailDeliveryReport(
    string Provider,
    bool Success,
    string Detail,
    DateTime TimestampUtc);

public record EmailProviderDiagnostics(
    bool HasSmtpCredentials,
    bool HasBrevoApiKey,
    bool HasResendApiKey,
    bool HasGmailWebhookUrl,
    bool HasGmailOAuthRefreshToken,
    string SmtpHost,
    int SmtpPort,
    string SmtpUser,
    string FromEmail,
    bool IsRenderEnvironment,
    EmailDeliveryReport? LastAttempt);

public class EmailService : IEmailService
{
    private static readonly HttpClient FallbackHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private EmailDeliveryReport? _lastAttempt;

    public EmailService(IConfiguration config, ILogger<EmailService> logger, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateHttpClient()
        => _httpClientFactory != null ? _httpClientFactory.CreateClient("EmailApiClient") : FallbackHttpClient;

    private string GetFirstConfigOrEnv(string[] configKeys, string[] envKeys, string defaultValue = "")
    {
        foreach (var envKey in envKeys)
        {
            var envVal = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envVal))
                return envVal.Trim().Trim('"', '\'');
        }

        foreach (var cfgKey in configKeys)
        {
            var cfgVal = _config[cfgKey];
            if (!string.IsNullOrWhiteSpace(cfgVal))
                return cfgVal.Trim().Trim('"', '\'');
        }

        return defaultValue;
    }

    private string GetConfigOrEnv(string configKey, string envKey, string defaultValue = "")
        => GetFirstConfigOrEnv(new[] { configKey }, new[] { envKey }, defaultValue);

    private static string NormalizeSmtpPassword(string host, string pass)
    {
        var trimmed = pass.Trim();
        // Google App Passwords display as "xxxx xxxx xxxx xxxx" (19 chars with spaces) — strip spaces automatically
        if (host.Contains("gmail", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(trimmed, @"^(?:[a-zA-Z0-9]{4}\s+){3}[a-zA-Z0-9]{4}$"))
        {
            return Regex.Replace(trimmed, @"\s+", "");
        }
        return trimmed;
    }

    private static bool IsRenderRuntime()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER")) ||
           !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_SERVICE_ID"));

    public EmailProviderDiagnostics GetDiagnostics()
    {
        var smtpHost = GetFirstConfigOrEnv(new[] { "Smtp:Host" }, new[] { "SMTP_HOST", "Smtp__Host" }, "smtp.gmail.com");
        int.TryParse(GetFirstConfigOrEnv(new[] { "Smtp:Port" }, new[] { "SMTP_PORT", "Smtp__Port" }, "587"), out int smtpPort);
        if (smtpPort <= 0) smtpPort = 587;
        var smtpUser = GetFirstConfigOrEnv(new[] { "Smtp:User" }, new[] { "SMTP_USER", "Smtp__User" }, "loopmarketua@gmail.com");
        var smtpPass = GetFirstConfigOrEnv(new[] { "Smtp:Pass" }, new[] { "SMTP_PASS", "Smtp__Pass" });
        var smtpFrom = ExtractEmail(GetFirstConfigOrEnv(new[] { "Smtp:From" }, new[] { "SMTP_FROM", "Smtp__From" }, smtpUser));

        var brevoKey = GetFirstConfigOrEnv(new[] { "Brevo:ApiKey", "Email:BrevoApiKey" }, new[] { "BREVO_API_KEY" });
        var resendKey = GetFirstConfigOrEnv(new[] { "Resend:ApiKey", "Email:ResendApiKey" }, new[] { "RESEND_API_KEY" });
        var webhookUrl = GetFirstConfigOrEnv(new[] { "Email:WebhookUrl", "Gmail:WebhookUrl" }, new[] { "GMAIL_WEBHOOK_URL", "EMAIL_WEBHOOK_URL" });
        var gmailRefresh = GetFirstConfigOrEnv(new[] { "Gmail:RefreshToken" }, new[] { "GMAIL_REFRESH_TOKEN" });
        var googleClientId = GetFirstConfigOrEnv(new[] { "Authentication:Google:ClientId" }, new[] { "GOOGLE_CLIENT_ID" });
        var googleSecret = GetFirstConfigOrEnv(new[] { "Authentication:Google:ClientSecret" }, new[] { "GOOGLE_CLIENT_SECRET" });

        return new EmailProviderDiagnostics(
            HasSmtpCredentials: !string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass),
            HasBrevoApiKey: !string.IsNullOrWhiteSpace(brevoKey),
            HasResendApiKey: !string.IsNullOrWhiteSpace(resendKey),
            HasGmailWebhookUrl: !string.IsNullOrWhiteSpace(webhookUrl),
            HasGmailOAuthRefreshToken: !string.IsNullOrWhiteSpace(gmailRefresh) && !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleSecret),
            SmtpHost: smtpHost,
            SmtpPort: smtpPort,
            SmtpUser: smtpUser,
            FromEmail: string.IsNullOrWhiteSpace(smtpFrom) ? smtpUser : smtpFrom,
            IsRenderEnvironment: IsRenderRuntime(),
            LastAttempt: _lastAttempt);
    }

    public Task<bool> SendDiagnosticTestAsync(string toEmail)
    {
        var html = $@"<div style=""font-family:sans-serif;padding:24px;max-width:520px;border:1px solid #e2e8f0;border-radius:12px;"">
            <h2 style=""margin:0 0 12px;color:#0f172a;"">LoopMarket — Тест доставки пошти</h2>
            <p style=""margin:0 0 8px;color:#334155;"">Якщо ви отримали цей лист, поштова інтеграція платформи працює коректно.</p>
            <p style=""margin:0;color:#64748b;font-size:12px;"">Час перевірки (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>
        </div>";
        return SendEmailInternalAsync(toEmail, "LoopMarket: Тест поштового сервісу", html, "[DIAGNOSTIC] Test email");
    }

    private static string ExtractEmail(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var match = Regex.Match(raw, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        return match.Success ? match.Value : raw.Trim();
    }

    public async Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code)
    {
        var safeUserName = string.IsNullOrWhiteSpace(userName) ? "Користувачу" : WebUtility.HtmlEncode(userName);
        var subject = $"Код підтвердження LoopMarket: {code}";

        var htmlBody = $@"<!DOCTYPE html>
<html lang=""uk"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{subject}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; color: #1e293b;"">
    <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #f1f5f9; padding: 40px 15px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 540px; background: #ffffff; border-radius: 20px; overflow: hidden; box-shadow: 0 12px 35px rgba(15, 23, 42, 0.08); border: 1px solid #e2e8f0;"">
                    <tr>
                        <td style=""background: linear-gradient(135deg, #090d16 0%, #1e293b 100%); padding: 36px 30px; text-align: center;"">
                            <div style=""display: inline-block;"">
                                <span style=""font-size: 28px; font-weight: 900; color: #ffffff; letter-spacing: -0.5px;"">Loop<span style=""color: #10b981;"">Market</span></span>
                            </div>
                            <div style=""margin-top: 10px;"">
                                <span style=""display: inline-block; padding: 4px 12px; background: rgba(16, 185, 129, 0.15); border: 1px solid rgba(16, 185, 129, 0.35); border-radius: 20px; color: #34d399; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px;"">
                                    Підтвердження акаунта
                                </span>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 38px 36px 32px; text-align: center;"">
                            <h1 style=""margin: 0 0 14px; font-size: 22px; font-weight: 800; color: #0f172a; letter-spacing: -0.3px;"">
                                Підтвердження електронної пошти
                            </h1>
                            <p style=""margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #475569; text-align: left;"">
                                Вітаємо, <b>{safeUserName}</b>!<br>
                                Дякуємо за реєстрацію на платформі <b>LoopMarket</b>. Для захисту вашого акаунта та завершення реєстрації введіть цей 6-значний одноразовий код:
                            </p>
                            <div style=""background: linear-gradient(135deg, #f0fdf4 0%, #eff6ff 100%); border: 2px dashed #2563eb; border-radius: 16px; padding: 22px 28px; margin: 24px 0; text-align: center;"">
                                <div style=""font-size: 12px; font-weight: 700; text-transform: uppercase; color: #64748b; letter-spacing: 1.5px; margin-bottom: 8px;"">
                                    Ваш одноразовий код підтвердження:
                                </div>
                                <div style=""font-family: 'SF Pro Display', Consolas, Monaco, monospace; font-size: 38px; font-weight: 900; letter-spacing: 10px; color: #1e40af; text-shadow: 0 1px 2px rgba(30, 64, 175, 0.15);"">
                                    {code}
                                </div>
                                <div style=""margin-top: 10px; font-size: 12px; color: #64748b;"">
                                    Код дійсний протягом <b>15 хвилин</b>
                                </div>
                            </div>
                            <p style=""margin: 0 0 24px; font-size: 13px; line-height: 1.5; color: #64748b; text-align: left;"">
                                Введіть цей код на сторінці реєстрації. Після введення всіх 6 цифр система автоматично активує ваш профіль та відкриє кабінет.
                            </p>
                            <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 14px 18px; text-align: left;"">
                                <div style=""font-size: 12px; font-weight: 700; color: #334155; margin-bottom: 4px;"">
                                    Правила безпеки:
                                </div>
                                <div style=""font-size: 12px; color: #64748b; line-height: 1.5;"">
                                    • Ніколи не повідомляйте цей код третім особам. Співробітники LoopMarket ніколи його не запитують.<br>
                                    • Якщо ви не реєструвалися на LoopMarket, просто проігноруйте цей лист.
                                </div>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 24px 30px; text-align: center;"">
                            <p style=""margin: 0 0 8px; font-size: 12px; font-weight: 600; color: #64748b;"">
                                LoopMarket — Торговельний майданчик нового покоління
                            </p>
                            <p style=""margin: 0; font-size: 11px; color: #94a3b8;"">
                                Потрібна допомога? Напишіть нам: <a href=""mailto:loopmarketua@gmail.com"" style=""color: #2563eb; text-decoration: none;"">loopmarketua@gmail.com</a><br>
                                © 2026 LoopMarket. Всі права захищені.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return await SendEmailInternalAsync(toEmail, subject, htmlBody, $"[EMAIL_VERIFICATION] Code is {code}");
    }

    public async Task<bool> SendPasswordResetCodeAsync(string toEmail, string userName, string code, string requestIp)
    {
        var safeUserName = string.IsNullOrWhiteSpace(userName) ? "Користувачу" : WebUtility.HtmlEncode(userName);
        var safeIp = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(requestIp) ? "невідомо" : requestIp);
        var subject = $"Відновлення пароля LoopMarket: {code}";

        var htmlBody = $@"<!DOCTYPE html>
<html lang=""uk"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{subject}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #f1f5f9; padding: 40px 15px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 540px; background: #ffffff; border-radius: 20px; overflow: hidden; box-shadow: 0 12px 35px rgba(15, 23, 42, 0.08); border: 1px solid #e2e8f0;"">
                    <tr>
                        <td style=""background: linear-gradient(135deg, #090d16 0%, #1e293b 100%); padding: 32px 30px; text-align: center;"">
                            <span style=""font-size: 26px; font-weight: 900; color: #ffffff;"">Loop<span style=""color: #10b981;"">Market</span></span>
                            <div style=""margin-top: 10px;"">
                                <span style=""display: inline-block; padding: 4px 12px; background: rgba(245, 158, 11, 0.16); border: 1px solid rgba(245, 158, 11, 0.4); border-radius: 20px; color: #fbbf24; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px;"">
                                    Безпека та відновлення пароля
                                </span>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 34px 32px;"">
                            <h1 style=""margin: 0 0 14px; font-size: 21px; font-weight: 800; color: #0f172a;"">
                                Запит на скидання пароля
                            </h1>
                            <p style=""margin: 0 0 20px; font-size: 15px; line-height: 1.6; color: #475569;"">
                                Вітаємо, <b>{safeUserName}</b>!<br>
                                Отримано запит на зміну пароля вашого акаунта LoopMarket (IP-адреса запиту: <code>{safeIp}</code>). Якщо це робите саме ви — введіть одноразовий код нижче:
                            </p>
                            <div style=""background: linear-gradient(135deg, #fffbeb 0%, #eff6ff 100%); border: 2px dashed #f59e0b; border-radius: 16px; padding: 20px; margin: 22px 0; text-align: center;"">
                                <div style=""font-size: 12px; font-weight: 700; text-transform: uppercase; color: #64748b; letter-spacing: 1.5px; margin-bottom: 8px;"">
                                    Одноразовий код відновлення:
                                </div>
                                <div style=""font-family: Consolas, Monaco, monospace; font-size: 36px; font-weight: 900; letter-spacing: 10px; color: #b45309;"">
                                    {code}
                                </div>
                                <div style=""margin-top: 8px; font-size: 12px; color: #64748b;"">
                                    Код дійсний <b>10 хвилин</b> • Максимум 5 спроб введення
                                </div>
                            </div>
                            <div style=""background: #fef2f2; border: 1px solid #fecaca; border-radius: 12px; padding: 14px 18px;"">
                                <div style=""font-size: 12px; font-weight: 800; color: #b91c1c; margin-bottom: 4px;"">
                                    Захист від викрадення акаунта:
                                </div>
                                <div style=""font-size: 12px; color: #7f1d1d; line-height: 1.5;"">
                                    • Якщо ви <b>НЕ</b> запитували скидання пароля — нікому не передавайте цей код, просто проігноруйте лист і негайно змініть пароль від пошти.<br>
                                    • Якщо до профілю прив'язано Telegram-бота <b>@LoopMarket_Verify_bot</b>, підтвердіть операцію або заблокуйте її в чаті з ботом.
                                </div>
                            </div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return await SendEmailInternalAsync(toEmail, subject, htmlBody, $"[PASSWORD_RESET] Code is {code}");
    }

    public async Task<bool> SendPasswordChangedAlertAsync(string toEmail, string userName, string requestIp)
    {
        var safeUserName = string.IsNullOrWhiteSpace(userName) ? "Користувачу" : WebUtility.HtmlEncode(userName);
        var safeIp = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(requestIp) ? "невідомо" : requestIp);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var subject = "Безпека LoopMarket: пароль акаунта успішно змінено";

        var htmlBody = $@"<!DOCTYPE html>
<html lang=""uk"">
<head><meta charset=""utf-8""><title>{subject}</title></head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;"">
    <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""padding: 36px 15px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 540px; background: #ffffff; border-radius: 20px; overflow: hidden; border: 1px solid #e2e8f0;"">
                    <tr>
                        <td style=""background: #090d16; padding: 28px; text-align: center;"">
                            <span style=""font-size: 24px; font-weight: 900; color: #ffffff;"">Loop<span style=""color: #10b981;"">Market</span></span>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 30px;"">
                            <h1 style=""margin: 0 0 12px; font-size: 20px; font-weight: 800; color: #0f172a;"">Пароль вашого акаунта змінено</h1>
                            <p style=""margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #475569;"">
                                Вітаємо, <b>{safeUserName}</b>!<br>
                                Пароль вашого профілю LoopMarket було успішно оновлено <b>{timestamp}</b> (IP: <code>{safeIp}</code>).
                            </p>
                            <p style=""margin: 0 0 18px; font-size: 14px; line-height: 1.6; color: #475569;"">
                                З міркувань безпеки всі попередні активні сесії входу були автоматично завершені.
                            </p>
                            <div style=""background: #fef2f2; border: 1px solid #fecaca; border-radius: 12px; padding: 14px; font-size: 12px; color: #7f1d1d;"">
                                <b>Це зробили не ви?</b> Негайно зв'яжіться зі службою підтримки LoopMarket: <b>@LoopMarketSupport</b> або <a href=""mailto:loopmarketua@gmail.com"">loopmarketua@gmail.com</a>.
                            </div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return await SendEmailInternalAsync(toEmail, subject, htmlBody, "[PASSWORD_CHANGED_ALERT] Sent");
    }

    private async Task<bool> SendEmailInternalAsync(string toEmail, string subject, string htmlBody, string devSummary)
    {
        var smtpHost = GetFirstConfigOrEnv(new[] { "Smtp:Host" }, new[] { "SMTP_HOST", "Smtp__Host" }, "smtp.gmail.com");
        var smtpPortStr = GetFirstConfigOrEnv(new[] { "Smtp:Port" }, new[] { "SMTP_PORT", "Smtp__Port" }, "587");
        var smtpUser = GetFirstConfigOrEnv(new[] { "Smtp:User" }, new[] { "SMTP_USER", "Smtp__User" }, "loopmarketua@gmail.com");
        var rawSmtpPass = GetFirstConfigOrEnv(new[] { "Smtp:Pass" }, new[] { "SMTP_PASS", "Smtp__Pass" });
        var smtpFrom = GetFirstConfigOrEnv(new[] { "Smtp:From" }, new[] { "SMTP_FROM", "Smtp__From" }, smtpUser);

        var cleanFrom = ExtractEmail(smtpFrom);
        if (string.IsNullOrWhiteSpace(cleanFrom)) cleanFrom = ExtractEmail(smtpUser);
        if (string.IsNullOrWhiteSpace(cleanFrom)) cleanFrom = "loopmarketua@gmail.com";

        int.TryParse(smtpPortStr, out int smtpPort);
        if (smtpPort <= 0) smtpPort = 587;

        bool anyProviderConfigured = false;
        var providerErrors = new List<string>();

        // 1. HTTPS Google Apps Script Gmail Relay (працює на Render Free через порт 443 напряму з Gmail)
        var webhookUrl = GetFirstConfigOrEnv(new[] { "Email:WebhookUrl", "Gmail:WebhookUrl" }, new[] { "GMAIL_WEBHOOK_URL", "EMAIL_WEBHOOK_URL" });
        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            anyProviderConfigured = true;
            var secret = GetFirstConfigOrEnv(new[] { "Email:WebhookSecret", "Gmail:WebhookSecret" }, new[] { "GMAIL_WEBHOOK_SECRET", "EMAIL_WEBHOOK_SECRET" });
            var (ok, err) = await TrySendViaWebhookAsync(webhookUrl, secret, toEmail, subject, htmlBody);
            if (ok)
            {
                _lastAttempt = new EmailDeliveryReport("GmailWebhookHttps", true, "Delivered via HTTPS Webhook", DateTime.UtcNow);
                return true;
            }
            providerErrors.Add($"GmailWebhookHttps: {err}");
        }

        // 2. HTTPS Gmail REST API OAuth2 (порт 443)
        var gmailRefreshToken = GetFirstConfigOrEnv(new[] { "Gmail:RefreshToken" }, new[] { "GMAIL_REFRESH_TOKEN" });
        var googleClientId = GetFirstConfigOrEnv(new[] { "Authentication:Google:ClientId" }, new[] { "GOOGLE_CLIENT_ID" });
        var googleClientSecret = GetFirstConfigOrEnv(new[] { "Authentication:Google:ClientSecret" }, new[] { "GOOGLE_CLIENT_SECRET" });
        if (!string.IsNullOrWhiteSpace(gmailRefreshToken) && !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            anyProviderConfigured = true;
            var (ok, err) = await TrySendViaGmailApiAsync(googleClientId, googleClientSecret, gmailRefreshToken, cleanFrom, toEmail, subject, htmlBody);
            if (ok)
            {
                _lastAttempt = new EmailDeliveryReport("GmailRestApi", true, "Delivered via Gmail HTTPS API", DateTime.UtcNow);
                return true;
            }
            providerErrors.Add($"GmailRestApi: {err}");
        }

        // 3. HTTPS Brevo API (300 безкоштовних листів/день з будь-якого Gmail sender на порт 443)
        var brevoKey = GetFirstConfigOrEnv(new[] { "Brevo:ApiKey", "Email:BrevoApiKey" }, new[] { "BREVO_API_KEY" });
        if (!string.IsNullOrWhiteSpace(brevoKey))
        {
            anyProviderConfigured = true;
            var (ok, err) = await TrySendViaBrevoAsync(brevoKey, cleanFrom, toEmail, subject, htmlBody);
            if (ok)
            {
                _lastAttempt = new EmailDeliveryReport("BrevoHttpsApi", true, "Delivered via Brevo HTTPS API", DateTime.UtcNow);
                return true;
            }
            providerErrors.Add($"BrevoHttpsApi: {err}");
        }

        // 4. HTTPS Resend API (порт 443)
        var resendKey = GetFirstConfigOrEnv(new[] { "Resend:ApiKey", "Email:ResendApiKey" }, new[] { "RESEND_API_KEY" });
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            anyProviderConfigured = true;
            var resendFrom = GetFirstConfigOrEnv(new[] { "Resend:From", "Email:ResendFrom" }, new[] { "RESEND_FROM" }, cleanFrom);
            var (ok, err) = await TrySendViaResendAsync(resendKey, resendFrom, toEmail, subject, htmlBody);
            if (ok)
            {
                _lastAttempt = new EmailDeliveryReport("ResendHttpsApi", true, "Delivered via Resend HTTPS API", DateTime.UtcNow);
                return true;
            }
            providerErrors.Add($"ResendHttpsApi: {err}");
        }

        // 5. Прямий SMTP (Gmail App Password) з перевіркою доступності порту та нормалізацією пробілів
        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(rawSmtpPass))
        {
            anyProviderConfigured = true;
            var smtpPass = NormalizeSmtpPassword(smtpHost, rawSmtpPass);
            // System.Net.Mail.SmtpClient підтримує STARTTLS (587), а не implicit TLS (465)
            int effectivePort = smtpPort == 465 && smtpHost.Contains("gmail", StringComparison.OrdinalIgnoreCase) ? 587 : smtpPort;

            var (ok, err) = await TrySendViaSmtpAsync(smtpHost, effectivePort, smtpUser, smtpPass, cleanFrom, toEmail, subject, htmlBody);
            if (ok)
            {
                _lastAttempt = new EmailDeliveryReport("Smtp", true, $"Delivered via {smtpHost}:{effectivePort}", DateTime.UtcNow);
                return true;
            }
            providerErrors.Add($"Smtp({smtpHost}:{effectivePort}): {err}");
        }

        var combinedError = providerErrors.Count > 0
            ? string.Join(" | ", providerErrors)
            : "Жодного поштового провайдера не налаштовано (ні SMTP_PASS, ні HTTPS API ключа BREVO_API_KEY / RESEND_API_KEY / GMAIL_WEBHOOK_URL).";

        _lastAttempt = new EmailDeliveryReport(
            anyProviderConfigured ? "Failed" : "NotConfigured",
            false,
            combinedError,
            DateTime.UtcNow);

        _logger.LogError("Email delivery failed to {Email}. Reason: {Reason}", toEmail, combinedError);

        var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
        if (!anyProviderConfigured &&
            (envName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
             envName.Equals("Testing", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"{devSummary} -> {toEmail}");
            return true;
        }

        return false;
    }

    private async Task<(bool Ok, string Error)> TrySendViaWebhookAsync(string webhookUrl, string secret, string toEmail, string subject, string htmlBody)
    {
        try
        {
            if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "Некоректний формат GMAIL_WEBHOOK_URL (потрібен повний https:// URL).");
            }

            if (parsedUri.Host.Contains("script.google.com", StringComparison.OrdinalIgnoreCase) &&
                parsedUri.AbsolutePath.TrimEnd('/').EndsWith("/dev", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Використано тестовий /dev URL Google Apps Script. Потрібен робочий /exec URL (Deploy -> Web app -> Manage deployments).");
            }

            var client = CreateHttpClient();
            var payload = JsonSerializer.Serialize(new
            {
                to = toEmail,
                subject,
                html = htmlBody,
                fromName = "LoopMarket",
                secret
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var res = await client.PostAsync(webhookUrl, content);
            var body = await res.Content.ReadAsStringAsync();
            var finalHost = res.RequestMessage?.RequestUri?.Host ?? "";
            var mediaType = res.Content.Headers.ContentType?.MediaType ?? "";
            var trimmedBody = body.TrimStart();

            if (!res.IsSuccessStatusCode)
            {
                return (false, $"HTTP {(int)res.StatusCode}: {TruncateDetail(body)}");
            }

            if (finalHost.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Google Apps Script перенаправив на accounts.google.com. У налаштуваннях розгортання встановіть 'Who has access: Anyone (Усі)'.");
            }

            if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                trimmedBody.StartsWith("<", StringComparison.Ordinal))
            {
                if (body.Contains("Script function not found: doPost", StringComparison.OrdinalIgnoreCase) || body.Contains("не найдена: doPost", StringComparison.OrdinalIgnoreCase) || body.Contains("не знайдено: doPost", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Google Apps Script повернув помилку: функцію doPost не знайдено (збережіть код Ctrl+S і створіть нову версію розгортання).");
                }
                if (body.Contains("ServiceLogin", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("Authorization is required", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Google Apps Script вимагає авторизації. Перевірте доступ 'Anyone (Усі)' та пройдіть підтвердження скрипта.");
                }
                return (false, $"Вебхук повернув HTML-сторінку замість JSON-відповіді: {TruncateDetail(StripHtmlTags(body))}");
            }

            if (trimmedBody.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmedBody);
                    if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.ValueKind == JsonValueKind.False)
                    {
                        var errText = doc.RootElement.TryGetProperty("error", out var errProp)
                            ? errProp.GetString()
                            : "Apps Script повернув ok=false";
                        return (false, string.IsNullOrWhiteSpace(errText) ? "Webhook returned ok=false" : errText!);
                    }
                    if (doc.RootElement.TryGetProperty("error", out var errOnlyProp) &&
                        errOnlyProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(errOnlyProp.GetString()))
                    {
                        return (false, errOnlyProp.GetString()!);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string StripHtmlTags(string html)
        => Regex.Replace(Regex.Replace(html, @"<[^>]+>", " "), @"\s+", " ").Trim();

    private static string TruncateDetail(string text, int maxLen = 220)
        => text.Length <= maxLen ? text : text[..maxLen] + "...";

    private async Task<(bool Ok, string Error)> TrySendViaGmailApiAsync(
        string clientId, string clientSecret, string refreshToken,
        string fromEmail, string toEmail, string subject, string htmlBody)
    {
        try
        {
            var client = CreateHttpClient();
            using var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            });
            using var tokenRes = await client.PostAsync("https://oauth2.googleapis.com/token", tokenReq);
            var tokenBody = await tokenRes.Content.ReadAsStringAsync();
            if (!tokenRes.IsSuccessStatusCode)
            {
                return (false, $"OAuth token refresh HTTP {(int)tokenRes.StatusCode}");
            }

            using var tokenDoc = JsonDocument.Parse(tokenBody);
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var atEl))
            {
                return (false, "Missing access_token in Google response");
            }
            var accessToken = atEl.GetString() ?? "";

            var encodedSubject = $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=";
            var mime = $"From: LoopMarket <{fromEmail}>\r\n" +
                       $"To: {toEmail}\r\n" +
                       $"Reply-To: loopmarketua@gmail.com\r\n" +
                       $"Subject: {encodedSubject}\r\n" +
                       "MIME-Version: 1.0\r\n" +
                       "Content-Type: text/html; charset=utf-8\r\n" +
                       "Content-Transfer-Encoding: base64\r\n\r\n" +
                       Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody));

            var rawUrlSafe = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            using var sendReq = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { raw = rawUrlSafe }), Encoding.UTF8, "application/json")
            };
            sendReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var sendRes = await client.SendAsync(sendReq);
            if (!sendRes.IsSuccessStatusCode)
            {
                var sendErr = await sendRes.Content.ReadAsStringAsync();
                return (false, $"Gmail API HTTP {(int)sendRes.StatusCode}: {sendErr}");
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string Error)> TrySendViaBrevoAsync(string apiKey, string fromEmail, string toEmail, string subject, string htmlBody)
    {
        try
        {
            var client = CreateHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            req.Headers.Add("api-key", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(new
            {
                sender = new { name = "LoopMarket", email = fromEmail },
                to = new[] { new { email = toEmail } },
                replyTo = new { name = "LoopMarket", email = "loopmarketua@gmail.com" },
                subject,
                htmlContent = htmlBody
            }), Encoding.UTF8, "application/json");

            using var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"HTTP {(int)res.StatusCode}: {err}");
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string Error)> TrySendViaResendAsync(string apiKey, string fromEmail, string toEmail, string subject, string htmlBody)
    {
        try
        {
            var client = CreateHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var formattedFrom = fromEmail.Contains('<') ? fromEmail : $"LoopMarket <{fromEmail}>";
            req.Content = new StringContent(JsonSerializer.Serialize(new
            {
                from = formattedFrom,
                to = new[] { toEmail },
                reply_to = "loopmarketua@gmail.com",
                subject,
                html = htmlBody
            }), Encoding.UTF8, "application/json");

            using var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"HTTP {(int)res.StatusCode}: {err}");
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string Error)> TrySendViaSmtpAsync(
        string smtpHost, int smtpPort, string smtpUser, string smtpPass,
        string cleanFrom, string toEmail, string subject, string htmlBody)
    {
        try
        {
            // Швидка перевірка TCP-доступності, щоб не чекати 15с на заблокованому фаєрволом порту Render Free
            using (var tcp = new TcpClient())
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await tcp.ConnectAsync(smtpHost, smtpPort, cts.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or SocketException)
                {
                    var renderNote = IsRenderRuntime()
                        ? " (На безкоштовному тарифі Render вихідні SMTP-порти 25/465/587 заблоковано — підключіть BREVO_API_KEY, RESEND_API_KEY або GMAIL_WEBHOOK_URL через HTTPS 443)"
                        : "";
                    return (false, $"TCP connect failed to {smtpHost}:{smtpPort}: {ex.Message}{renderNote}");
                }
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = smtpPort != 25,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 8000
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(cleanFrom, "LoopMarket"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.ReplyToList.Add(new MailAddress("loopmarketua@gmail.com", "LoopMarket"));
            mail.Headers.Add("X-Auto-Response-Suppress", "All");
            mail.Headers.Add("Auto-Submitted", "auto-generated");
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Email sent via SMTP to {Email} ({Subject})", toEmail, subject);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
