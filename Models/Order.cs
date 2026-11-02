using System;
using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.Models;

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Shipped = "Shipped";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public class Order
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public int BuyerId { get; set; }
    public User? Buyer { get; set; }

    public int SellerId { get; set; }
    public User? Seller { get; set; }

    public decimal TotalPrice { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = OrderStatus.Pending;

    [Required]
    [MaxLength(100)]
    public string BuyerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string BuyerPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DeliveryCity { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string DeliveryBranch { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Накладений платіж";

    [MaxLength(50)]
    public string? TrackingNumber { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
