using System;

namespace TradeStorm.Api.Models;

public static class ComplaintStatus
{
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";
}

public class Complaint
{
    public int Id { get; set; }

    public int ReporterId { get; set; }
    public User? Reporter { get; set; }

    public int TargetUserId { get; set; }
    public User? TargetUser { get; set; }

    public int? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = ComplaintStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ResolutionNotes { get; set; }
}
