using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.Models;

public class Listing
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    // 🔥 Новое поле для хранения даты и времени создания объявления
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int SellerId { get; set; }
    public User? Seller { get; set; }
    public string? Condition { get; set; }
    public string? Location { get; set; }
    public bool IsShippingAvailable { get; set; }
}