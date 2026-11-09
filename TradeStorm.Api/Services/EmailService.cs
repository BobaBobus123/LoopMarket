using System;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TradeStorm.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string GetConfigOrEnv(string configKey, string envKey, string defaultValue = "")
    {
        // 1. Пріоритет: змінні середовища (Environment Variables)
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envVal)) return envVal.Trim();

        // 2. Якщо в Environment порожньо — перевіряємо Configuration (appsettings.*)
        var cfgVal = _config[configKey];
        if (!string.IsNullOrWhiteSpace(cfgVal)) return cfgVal.Trim();

        return defaultValue;
    }

    private static string ExtractEmail(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var match = Regex.Match(raw, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        return match.Success ? match.Value : raw.Trim();
    }

    public async Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code)
    {
        var smtpHost = GetConfigOrEnv("Smtp:Host", "SMTP_HOST", "smtp.gmail.com");
        var smtpPortStr = GetConfigOrEnv("Smtp:Port", "SMTP_PORT", "587");
        var smtpUser = GetConfigOrEnv("Smtp:User", "SMTP_USER", "loopmarketua@gmail.com");
        var smtpPass = GetConfigOrEnv("Smtp:Pass", "SMTP_PASS");
        var smtpFrom = GetConfigOrEnv("Smtp:From", "SMTP_FROM", "loopmarketua@gmail.com");

        int.TryParse(smtpPortStr, out int smtpPort);
        if (smtpPort <= 0) smtpPort = 587;

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
                                    🛡️ Офіційне сповіщення безпеки
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
                                Вітаємо, <b>{safeUserName}</b>! 👋<br>
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
                                    ⏱️ Код дійсний протягом <b>15 хвилин</b>
                                </div>
                            </div>
                            <p style=""margin: 0 0 24px; font-size: 13px; line-height: 1.5; color: #64748b; text-align: left;"">
                                💡 Введіть цей код на сторінці реєстрації. Після введення всіх 6 цифр система автоматично активує ваш профіль та відкриє кабінет.
                            </p>
                            <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 14px 18px; text-align: left;"">
                                <div style=""font-size: 12px; font-weight: 700; color: #334155; margin-bottom: 4px;"">
                                    🔒 Правила безпеки:
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

        // Якщо вказано пароль SMTP — відправляємо реальний лист через Gmail SMTP
        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            try
            {
                var cleanFrom = ExtractEmail(smtpFrom);
                if (string.IsNullOrWhiteSpace(cleanFrom)) cleanFrom = ExtractEmail(smtpUser);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = smtpPort != 25,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(cleanFrom, "LoopMarket Security"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("Verification email sent via SMTP to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email via SMTP to {Email}", toEmail);
            }
        }
        else
        {
            _logger.LogWarning("SMTP credentials not fully configured (Host: {Host}, User: {User}, HasPass: {HasPass})",
                smtpHost, smtpUser, !string.IsNullOrWhiteSpace(smtpPass));
        }

        // Fallback / Development mode
        Console.WriteLine($"[EMAIL_VERIFICATION] Simulated sending email to {toEmail}: Code is {code}");
        _logger.LogInformation("[DEV EMAIL] To: {Email}, Code: {Code}", toEmail, code);
        return true;
    }
}
