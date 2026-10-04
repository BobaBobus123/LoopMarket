using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Email обов'язковий")]
    [EmailAddress(ErrorMessage = "Невірний формат email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обов'язковий")]
    public string Password { get; set; } = string.Empty;
}