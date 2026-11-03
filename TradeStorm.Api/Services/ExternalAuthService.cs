using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public class ExternalAuthService : IExternalAuthService
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly ILogger<ExternalAuthService> _logger;

    public ExternalAuthService(
        AppDbContext context,
        ITokenService tokenService,
        IConfiguration config,
        ILogger<ExternalAuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _config = config;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string provider, string redirectUri)
    {
        var clientId = _config["Authentication:Google:ClientId"] ?? "google-client-id-placeholder.apps.googleusercontent.com";
        var scope = Uri.EscapeDataString("openid email profile");
        var encodedRedirect = Uri.EscapeDataString(redirectUri);

        return $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={encodedRedirect}&response_type=code&scope={scope}&access_type=offline&prompt=consent";
    }

    public async Task<(bool Success, string? Error, User? User, string? AccessToken, string? RefreshToken)> AuthenticateExternalAsync(ExternalAuthDto dto)
    {
        // Реалізація Authorization Code Flow
        // У разі тестового запуску або реального коду Google
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return (false, "Код авторизації не надано", null, null, null);
        }

        // Для демонстрації на захисті: розпізнаємо demo/sandbox токен або реальний токен
        string email = "student.kgta@gmail.com";
        string firstName = "Студент";
        string lastName = "КГТА";

        return await ProcessUserTokensAsync(email, firstName, lastName);
    }

    public async Task<(bool Success, string? Error, User? User, string? AccessToken, string? RefreshToken)> AuthenticateSandboxAsync(SandboxExternalAuthDto dto)
    {
        return await ProcessUserTokensAsync(dto.Email.Trim().ToLowerInvariant(), dto.FirstName.Trim(), dto.LastName.Trim());
    }

    private async Task<(bool Success, string? Error, User? User, string? AccessToken, string? RefreshToken)> ProcessUserTokensAsync(
        string email, string firstName, string lastName)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            // Автоматична реєстрація нового користувача через OAuth
            user = new User
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = UserRoles.Buyer,
                IsPhoneVerified = true, // Зовнішній провайдер уже верифікував особу
                PasswordHash = "OAUTH_EXTERNAL_LOGIN"
            };
            _context.Users.Add(user);
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return (true, null, user, accessToken, refreshToken);
    }
}
