using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public interface IExternalAuthService
{
    bool IsConfigured(string provider);
    string GetAuthorizationUrl(string provider, string redirectUri);
    Task<(bool Success, string? Error, User? User, string? AccessToken, string? RefreshToken)> AuthenticateExternalAsync(ExternalAuthDto dto);
    Task<(bool Success, string? Error, User? User, string? AccessToken, string? RefreshToken)> AuthenticateSandboxAsync(SandboxExternalAuthDto dto);
}
