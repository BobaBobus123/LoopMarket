using System;

namespace TradeStorm.Api.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Seller = "Seller";
    public const string Buyer = "Buyer";
}

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Buyer;
    public string? PhoneNumber { get; set; }
    public bool IsPhoneVerified { get; set; } = false;
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiresAt { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public long TelegramChatId { get; set; }
    public bool ReceiveTelegramNotifications { get; set; } = true;

    // Refresh Token для безпечного оновлення сесії (Етап 5)
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Модерація та блокування (Оператор/Адмін)
    public bool IsBanned { get; set; } = false;
    public string? BanReason { get; set; }
    public DateTime? MutedUntil { get; set; }

    // Преміум-підписка (Loop PRO / Бренд)
    public bool IsPremium { get; set; } = false;
    public DateTime? PremiumUntil { get; set; }
    public string? PremiumPlan { get; set; }
}
