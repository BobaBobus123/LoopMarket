using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class CreateListingDto
{
    [Required(ErrorMessage = "Назва є обов'язковою")]
    [MaxLength(100, ErrorMessage = "Назва не може перевищувати 100 символів")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опис є обов'язковим")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Ціна має бути більшою за нуль")]
    public decimal Price { get; set; }

    public int CategoryId { get; set; }
    public int SellerId { get; set; }
}