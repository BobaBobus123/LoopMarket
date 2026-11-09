using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class UpdateListingDto
{
    [Required(ErrorMessage = "Назва оголошення є обов'язковою")]
    [MaxLength(100, ErrorMessage = "Назва не може перевищувати 100 символів")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опис оголошення є обов'язковим")]
    public string Description { get; set; } = string.Empty;

    [Range(0.00, 1000000, ErrorMessage = "Ціна має бути невід'ємною")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Категорія є обов'язковою")]
    public int CategoryId { get; set; }

    public List<string>? ImageUrls { get; set; }

    public string? VideoUrl { get; set; }

    public string? Condition { get; set; }

    public string? Location { get; set; }

    public bool IsShippingAvailable { get; set; }

    public string? Status { get; set; }
}
