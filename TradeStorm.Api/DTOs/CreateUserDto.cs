using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class CreateUserDto
{
    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string PhoneNumber { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}