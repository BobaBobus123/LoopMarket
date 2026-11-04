using System;

namespace TradeStorm.Api.Models;

public class AdminAuditLog
{
    public int Id { get; set; }
    public int AdminId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public int? TargetId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
