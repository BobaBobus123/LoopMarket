using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "Email є обов'язковим")]
    [EmailAddress(ErrorMessage = "Невірний формат пошти")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль є обов'язковим")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Група є обов'язковою")]
    public string Group { get; set; } = string.Empty;
}