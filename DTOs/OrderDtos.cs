using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class CreateOrderDto
{
    [Required]
    public int ListingId { get; set; }

    [Required(ErrorMessage = "Вкажіть ПІБ отримувача")]
    [MaxLength(100)]
    public string BuyerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть контактний телефон")]
    [Phone]
    [MaxLength(30)]
    public string BuyerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть місто доставки")]
    [MaxLength(100)]
    public string DeliveryCity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть відділення пошти")]
    [MaxLength(150)]
    public string DeliveryBranch { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Накладений платіж";

    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TrackingNumber { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public string? ListingImageUrl { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerPhone { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryBranch { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public string? SellerName { get; set; }
    public string? SellerPhone { get; set; }
}
