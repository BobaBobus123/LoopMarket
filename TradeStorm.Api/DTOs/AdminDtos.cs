namespace TradeStorm.Api.DTOs;

public class BanUserDto
{
    public bool IsBanned { get; set; }
    public string? Reason { get; set; }
}

public class UpdateUserAdminDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsPhoneVerified { get; set; }
    public bool? IsEmailVerified { get; set; }
    public long? TelegramChatId { get; set; }
}

public class CreateComplaintDto
{
    public int TargetUserId { get; set; }
    public int? ListingId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class ResolveComplaintDto
{
    public string Status { get; set; } = "Resolved"; // Resolved, Dismissed
    public string? ResolutionNotes { get; set; }
    public bool BanTargetUser { get; set; } = false;
    public string? BanReason { get; set; }
}

public class ReplyComplaintDto
{
    public string Message { get; set; } = string.Empty;
    public bool MarkResolved { get; set; } = true;
    public bool SendInternalChat { get; set; } = true;
    public bool SendTelegram { get; set; } = true;
}

public class AdminResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalListings { get; set; }
    public int TotalOrders { get; set; }
    public int PendingComplaints { get; set; }
    public int BannedUsersCount { get; set; }
    public decimal TotalTurnover { get; set; }
}

public class BroadcastDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool SendTelegram { get; set; } = true;
}

public class SiteAnnouncementDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "warning";
    public string TargetPage { get; set; } = "all";
    public string Placement { get; set; } = "top";
    public string? Duration { get; set; }
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MuteUserDto
{
    public int Minutes { get; set; } // 0 to unmute
}

public class ForceUpdateOrderDto
{
    public string? Status { get; set; }
    public string? TrackingNumber { get; set; }
    public decimal? TotalPrice { get; set; }
}

public class CategoryAdminDto
{
    public string Name { get; set; } = string.Empty;
}

public class AdminCreateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "Buyer";
    public bool IsPhoneVerified { get; set; } = false;
    public bool IsEmailVerified { get; set; } = false;
}

public class AdminToggleVerifyDto
{
    public bool? IsVerified { get; set; }
    public bool? IsEmailVerified { get; set; }
    public bool? IsPhoneVerified { get; set; }
}

public class AdminSendNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool SendTelegram { get; set; } = true;
}

public class AdminCreateListingDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string Condition { get; set; } = "Вживане";
    public string? Location { get; set; }
    public bool IsShippingAvailable { get; set; } = true;
    public int? SellerId { get; set; }
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsFeatured { get; set; } = false;
}
