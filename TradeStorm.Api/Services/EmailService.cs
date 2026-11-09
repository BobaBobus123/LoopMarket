using System;
using System.Net;
using System.Net.Mail;
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

    public async Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code)
    {
        var smtpHost = _config["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var smtpPortStr = _config["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
        var smtpUser = _config["Smtp:User"] ?? Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPass = _config["Smtp:Pass"] ?? Environment.GetEnvironmentVariable("SMTP_PASS");
        var smtpFrom = _config["Smtp:From"] ?? Environment.GetEnvironmentVariable("SMTP_FROM") ?? "security@loopmarket.com";

        int.TryParse(smtpPortStr, out int smtpPort);
        if (smtpPort <= 0) smtpPort = 587;

        var subject = $"Код підтвердження LoopMarket: {code}";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; color: #1e293b; }}
        .email-container {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 18px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); }}
        .header {{ background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 28px 32px; text-align: center; }}
        .logo {{ font-size: 24px; font-weight: 800; color: #ffffff; letter-spacing: -0.5px; }}
        .logo span {{ color: #10b981; }}
        .content {{ padding: 32px; text-align: center; }}
        .title {{ font-size: 20px; font-weight: 800; color: #0f172a; margin-top: 0; margin-bottom: 12px; }}
        .desc {{ font-size: 15px; color: #64748b; line-height: 1.5; margin-bottom: 24px; }}
        .code-box {{ display: inline-block; background: #eff6ff; border: 2px dashed #3b82f6; border-radius: 14px; padding: 16px 36px; font-size: 34px; font-weight: 900; letter-spacing: 8px; color: #1d4ed8; margin: 12px 0 24px 0; }}
        .warning {{ font-size: 13px; color: #94a3b8; line-height: 1.4; border-top: 1px solid #f1f5f9; padding-top: 20px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div class='logo'>Loop<span>Market</span></div>
        </div>
        <div class='content'>
            <h2 class='title'>Підтвердження електронної пошти</h2>
            <p class='desc'>Вітаємо, {System.Net.WebUtility.HtmlEncode(userName)}! Використайте цей одноразовий код для верифікації вашого акаунта на платформі LoopMarket:</p>
            <div class='code-box'>{code}</div>
            <p style='font-size: 14px; color: #64748b; margin: 0;'>⏱️ Код дійсний протягом <b>15 хвилин</b>.</p>
            <div class='warning'>
                🛡️ Якщо ви не запитували цей код, просто проігноруйте цей лист.<br>
                Співробітники LoopMarket ніколи не запитують ваш пароль або одноразові коди.
            </div>
        </div>
    </div>
</body>
</html>";

        // If SMTP credentials exist, send via SMTP
        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            try
            {
                var effectiveFrom = (!string.IsNullOrWhiteSpace(smtpFrom) && smtpFrom != "security@loopmarket.com") 
                    ? smtpFrom 
                    : smtpUser;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = smtpPort != 25
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(effectiveFrom, "LoopMarket Security"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("Verification email sent to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email via SMTP to {Email}", toEmail);
            }
        }

        // Fallback / Development mode: log code
        Console.WriteLine($"[EMAIL_VERIFICATION] Simulated sending email to {toEmail}: Code is {code}");
        _logger.LogInformation("[DEV EMAIL] To: {Email}, Code: {Code}", toEmail, code);
        return true;
    }
}
