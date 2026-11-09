using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class SendCodeDto
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class VerifyEmailCodeDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
