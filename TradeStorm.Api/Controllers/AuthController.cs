using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using TradeStorm.Api.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthRateLimit")]
public class AuthController : ControllerBase
{
    private void SetAuthCookies(string? token, string? refreshToken)
    {
        if (string.IsNullOrEmpty(token)) return;
        try
        {
            var isHttps = Request.IsHttps;
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddMinutes(60),
                Path = "/"
            };
            Response.Cookies.Append("loopToken", token, cookieOptions);

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = isHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Path = "/"
                };
                Response.Cookies.Append("loopRefreshToken", refreshToken, refreshOptions);
            }
        }
        catch { }
    }

    private void ClearAuthCookies()
    {
        try
        {
            Response.Cookies.Delete("loopToken", new CookieOptions { Path = "/" });
            Response.Cookies.Delete("loopRefreshToken", new CookieOptions { Path = "/" });
        }
        catch { }
    }
    private readonly IExternalAuthService _externalAuthService;
    private readonly ICaptchaService _captchaService;

    public AuthController(IExternalAuthService externalAuthService, ICaptchaService captchaService)
    {
        _externalAuthService = externalAuthService;
        _captchaService = captchaService;
    }

    /// <summary>
    /// Генерація криптографічного завдання капчі (BotShield)
    /// </summary>
    [HttpGet("captcha")]
    public IActionResult GetCaptcha()
    {
        var (token, svg, question) = _captchaService.GenerateCaptcha();
        return Ok(new
        {
            token,
            svg,
            question
        });
    }

    public class VerifyCaptchaDto
    {
        public string? Token { get; set; }
        public string? Answer { get; set; }
    }

    /// <summary>
    /// Валідація відповіді користувача на капчу
    /// </summary>
    [HttpPost("verify-captcha")]
    public IActionResult VerifyCaptcha([FromBody] VerifyCaptchaDto dto)
    {
        bool isValid = _captchaService.ValidateCaptcha(dto.Token, dto.Answer);
        if (!isValid) return BadRequest(new { valid = false, error = "Неправильна відповідь або час дії капчі минув" });
        return Ok(new { valid = true });
    }

    /// <summary>
    /// Отримати URL для перенаправлення на Google OAuth 2.0 (Етап 7 - Authorization Code Flow)
    /// </summary>
    [HttpGet("google/url")]
    public IActionResult GetGoogleAuthUrl([FromQuery] string redirectUri = "http://localhost:65083/login.html")
    {
        var isConfigured = _externalAuthService.IsConfigured("Google");
        var url = _externalAuthService.GetAuthorizationUrl("Google", redirectUri);
        return Ok(new 
        { 
            provider = "Google", 
            isConfigured = isConfigured,
            authorizationUrl = url,
            note = isConfigured
                ? "Дійсний Google ClientId налаштовано."
                : "Використовується демонстраційний ClientId. Для захисту лабораторних робіт використовуйте вбудований OAuth Sandbox."
        });
    }

    /// <summary>
    /// Завершення OAuth авторизації за отриманим Authorization Code
    /// </summary>
    [HttpPost("external/callback")]
    public async Task<IActionResult> ExternalCallback([FromBody] ExternalAuthDto dto)
    {
        var (success, error, user, accessToken, refreshToken) = await _externalAuthService.AuthenticateExternalAsync(dto);
        if (!success || user == null)
        {
            return BadRequest(new { error = error ?? "Помилка зовнішньої авторизації" });
        }

        if (!user.IsPhoneVerified && user.Role != UserRoles.Admin)
        {
            return Ok(new
            {
                isPhoneVerified = false,
                userId = user.Id,
                email = user.Email,
                role = user.Role,
                message = "Для безпеки маркетплейсу підтвердіть свій номер телефону через Telegram"
            });
        }

        SetAuthCookies(accessToken, refreshToken);
        return Ok(new
        {
            isPhoneVerified = true,
            token = accessToken,
            refreshToken = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}".Trim(),
                role = user.Role,
                avatarUrl = user.AvatarUrl
            }
        });
    }

    /// <summary>
    /// Демонстраційний Sandbox OAuth вхід для захисту лабораторних робіт перед викладачем (Етап 7)
    /// </summary>
    [HttpPost("external/sandbox")]
    public async Task<IActionResult> ExternalSandboxLogin([FromBody] SandboxExternalAuthDto dto)
    {
        var (success, error, user, accessToken, refreshToken) = await _externalAuthService.AuthenticateSandboxAsync(dto);
        if (!success || user == null)
        {
            return BadRequest(new { error = error ?? "Помилка зовнішньої авторизації" });
        }

        if (!user.IsPhoneVerified && user.Role != UserRoles.Admin)
        {
            return Ok(new
            {
                isPhoneVerified = false,
                userId = user.Id,
                email = user.Email,
                role = user.Role,
                message = "Для безпеки маркетплейсу підтвердіть свій номер телефону через Telegram"
            });
        }

        SetAuthCookies(accessToken, refreshToken);
        return Ok(new
        {
            isPhoneVerified = true,
            token = accessToken,
            refreshToken = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}".Trim(),
                role = user.Role,
                avatarUrl = user.AvatarUrl
            }
        });
    }

    /// <summary>
    /// Оновлення Access Token за допомогою валідного Refresh Token (Етап 5)
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshDto dto, [FromServices] AppDbContext db, [FromServices] ITokenService tokenService)
    {
        var principal = tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);
        if (principal == null)
            return BadRequest(new { error = "Недійсний токен доступу" });

        var userIdString = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out int userId))
            return BadRequest(new { error = "Недійсний ідентифікатор користувача в токені" });

        var user = await db.Users.FindAsync(userId);
        if (user == null || user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return BadRequest(new { error = "Недійсний або прострочений Refresh токен" });

        var newAccessToken = tokenService.GenerateAccessToken(user);
        var newRefreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync();

        SetAuthCookies(newAccessToken, newRefreshToken);
        return Ok(new
        {
            token = newAccessToken,
            refreshToken = newRefreshToken
        });
    }

    /// <summary>
    /// Відкликання токенів / завершення сесії (Revocation/Logout) (Етап 5)
    /// </summary>
    [HttpPost("revoke")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Revoke([FromServices] AppDbContext db)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await db.SaveChangesAsync();
        }

        return Ok(new { message = "Сесію успішно завершено. Refresh Token відкликано." });
    }

    /// <summary>
    /// Ініціалізація сесії авторизації через Telegram-бота (Етап 6 & 7)
    /// </summary>
    [HttpGet("telegram/status")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [EnableRateLimiting("PollingLimit")]
    public IActionResult GetTelegramStatus([FromServices] TelegramBotService botService)
    {
        return Ok(new
        {
            configured = botService.HasBotToken,
            webhookMode = botService.IsWebhookMode,
            mode = !botService.HasBotToken ? "disabled" : (botService.IsWebhookMode ? "webhook" : "polling"),
            botUsername = "LoopMarket_Verify_bot"
        });
    }

    [HttpGet("telegram/start")]
    public IActionResult StartTelegramLogin([FromServices] TelegramBotService botService)
    {
        var code = "login_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var session = new TelegramLoginSession { Code = code };
        TelegramBotService.LoginSessions[code] = session;

        return Ok(new
        {
            code = code,
            botConfigured = botService.HasBotToken,
            botUsername = "LoopMarket_Verify_bot",
            deepLink = $"https://t.me/LoopMarket_Verify_bot?start={code}"
        });
    }

    /// <summary>
    /// Перевірка статусу підтвердження входу через Telegram
    /// </summary>
    [HttpGet("telegram/check-login")]
    [EnableRateLimiting("PollingLimit")]
    public IActionResult CheckTelegramLogin([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code) || !TelegramBotService.LoginSessions.TryGetValue(code, out var session))
        {
            return Ok(new { approved = false, error = "Сесію не знайдено або термін дії вичерпано" });
        }

        if (session.IsApproved && !string.IsNullOrEmpty(session.Token))
        {
            TelegramBotService.LoginSessions.TryRemove(code, out _);
            return Ok(new
            {
                approved = true,
                token = session.Token,
                refreshToken = session.RefreshToken,
                user = new
                {
                    email = session.UserEmail,
                    name = session.UserName,
                    role = session.UserRole
                }
            });
        }

        return Ok(new { approved = false });
    }
}
