using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IExternalAuthService _externalAuthService;

    public AuthController(IExternalAuthService externalAuthService)
    {
        _externalAuthService = externalAuthService;
    }

    /// <summary>
    /// Отримати URL для перенаправлення на Google OAuth 2.0 (Етап 7 - Authorization Code Flow)
    /// </summary>
    [HttpGet("google/url")]
    public IActionResult GetGoogleAuthUrl([FromQuery] string redirectUri = "http://localhost:65083/login.html")
    {
        var url = _externalAuthService.GetAuthorizationUrl("Google", redirectUri);
        return Ok(new { provider = "Google", authorizationUrl = url });
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

        return Ok(new
        {
            token = accessToken,
            refreshToken = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}",
                role = user.Role
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

        return Ok(new
        {
            token = accessToken,
            refreshToken = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}",
                role = user.Role
            }
        });
    }
}
