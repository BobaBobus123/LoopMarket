using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class ChangeRoleDto
{
    [Required]
    public string Role { get; set; } = string.Empty;
}
