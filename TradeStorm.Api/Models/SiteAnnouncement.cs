using System;

namespace TradeStorm.Api.Models;

public class SiteAnnouncement
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "warning"; // "info", "warning", "danger"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
