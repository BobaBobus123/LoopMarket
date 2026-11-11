using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class StartPasswordResetDto
{
    [Required(ErrorMessage = "Введіть електронну пошту")]
    [EmailAddress(ErrorMessage = "Некоректний формат електронної пошти")]
    public string Email { get; set; } = string.Empty;

    public string? HoneypotTrap { get; set; }
    public string? CaptchaToken { get; set; }
    public string? CaptchaAnswer { get; set; }
}

public class VerifyPasswordResetDto
{
    [Required]
    public string SessionToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть 6-значний код підтвердження")]
    public string Code { get; set; } = string.Empty;

    public string? PhoneLastDigits { get; set; }
}

public class ConfirmPasswordResetDto
{
    [Required]
    public string SessionToken { get; set; } = string.Empty;

    [Required]
    public string ResetProofToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть новий пароль")]
    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
    public string NewPassword { get; set; } = string.Empty;

    public string? ConfirmPassword { get; set; }
}

public class ResendPasswordResetDto
{
    [Required]
    public string SessionToken { get; set; } = string.Empty;
}
