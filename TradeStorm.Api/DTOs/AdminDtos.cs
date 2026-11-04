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
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "warning";
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
