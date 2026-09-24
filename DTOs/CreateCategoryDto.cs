using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class CreateCategoryDto
{
    [Required(ErrorMessage = "Назва категорії є обов'язковою")]
    [MaxLength(50, ErrorMessage = "Назва не може перевищувати 50 символів")]
    public string Name { get; set; } = string.Empty;
}