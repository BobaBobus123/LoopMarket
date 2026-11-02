using System.Security.Claims;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
