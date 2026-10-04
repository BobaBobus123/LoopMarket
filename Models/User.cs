namespace TradeStorm.Api.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // Було Student
    public string? PhoneNumber { get; set; }
    public bool IsPhoneVerified { get; set; } = false;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public long TelegramChatId { get; set; }
    public bool ReceiveTelegramNotifications { get; set; } = true;
}