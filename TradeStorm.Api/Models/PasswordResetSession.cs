using System;
using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.Models;

public class PasswordResetSession
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [Required]
    [MaxLength(128)]
    public string SessionToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string OtpHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ProofTokenHash { get; set; }

    public int FailedAttempts { get; set; } = 0;

    public bool RequiresPhoneCheck { get; set; } = false;
    public bool RequiresTelegramApproval { get; set; } = false;

    public bool IsEmailVerified { get; set; } = false;
    public bool IsPhoneVerified { get; set; } = false;
    public bool IsTelegramApproved { get; set; } = false;
    public bool IsRevoked { get; set; } = false;
    public bool IsCompleted { get; set; } = false;

    [MaxLength(64)]
    public string? RequestIp { get; set; }

    [MaxLength(256)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSentAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
