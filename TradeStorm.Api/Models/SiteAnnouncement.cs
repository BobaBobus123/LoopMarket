using System;

namespace TradeStorm.Api.Models;

public class SiteAnnouncement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "warning"; // "promo", "info", "warning", "danger"
    public string TargetPage { get; set; } = "all"; // "all", "home", "details", "services"
    public string Placement { get; set; } = "top"; // "top", "catalog", "modal"
    public DateTime? ExpiresAt { get; set; }
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
