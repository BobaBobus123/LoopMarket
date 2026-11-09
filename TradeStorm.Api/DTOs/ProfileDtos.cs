namespace TradeStorm.Api.DTOs;

public class UpdateProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public bool? ReceiveTelegramNotifications { get; set; }
}

public class AvatarDto
{
    public string AvatarUrl { get; set; } = string.Empty;
}
