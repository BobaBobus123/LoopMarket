using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class TokenRefreshDto
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
