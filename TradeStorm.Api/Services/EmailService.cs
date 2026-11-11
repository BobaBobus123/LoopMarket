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
        var smtpHost = GetConfigOrEnv("Smtp:Host", "SMTP_HOST", "smtp.gmail.com");
        var smtpPortStr = GetConfigOrEnv("Smtp:Port", "SMTP_PORT", "587");
        var smtpUser = GetConfigOrEnv("Smtp:User", "SMTP_USER", "loopmarketua@gmail.com");
        var smtpPass = GetConfigOrEnv("Smtp:Pass", "SMTP_PASS");
        var smtpFrom = GetConfigOrEnv("Smtp:From", "SMTP_FROM", "loopmarketua@gmail.com");

        int.TryParse(smtpPortStr, out int smtpPort);
        if (smtpPort <= 0) smtpPort = 587;

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
        Console.WriteLine($"{devSummary} -> {toEmail}");
        _logger.LogInformation("[DEV EMAIL] To: {Email}, {Summary}", toEmail, devSummary);
        return true;
    }
}
