using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.DTOs;

public class ExternalAuthDto
{
    [Required]
    public string Provider { get; set; } = "Google"; // Google, GitHub, Sandbox

    [Required]
    public string Code { get; set; } = string.Empty;

    public string? RedirectUri { get; set; }
}

public class SandboxExternalAuthDto
{
    [Required]
    public string Provider { get; set; } = "Google";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}
