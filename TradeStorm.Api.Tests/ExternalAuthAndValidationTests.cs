using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using Xunit;

namespace TradeStorm.Api.Tests;

/// <summary>
/// Інтеграційні тести для Етапу 7: «Зовнішня автентифікація (OAuth), керування секретами та валідація»
/// </summary>
public class ExternalAuthAndValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExternalAuthAndValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OAuth_GetGoogleUrl_ShouldReturnValidAuthorizationUrl()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/google/url?redirectUri=http://localhost:65083/login.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("authorizationUrl").GetString();

        Assert.NotNull(url);
        Assert.Contains("accounts.google.com", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("scope=", url);
    }

    [Fact]
    public async Task OAuth_SandboxLogin_ShouldReturnValidJwtAndUser()
    {
        var client = _factory.CreateClient();
        var sandboxDto = new SandboxExternalAuthDto
        {
            Provider = "Google",
            Email = $"oauth_user_{Guid.NewGuid():N}@gmail.com",
            FirstName = "Тарас",
            LastName = "Шевченко"
        };

        var response = await client.PostAsJsonAsync("/api/auth/external/sandbox", sandboxDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var token = doc.RootElement.GetProperty("token").GetString();
        var refreshToken = doc.RootElement.GetProperty("refreshToken").GetString();
        var userEmail = doc.RootElement.GetProperty("user").GetProperty("email").GetString();

        Assert.NotNull(token);
        Assert.NotNull(refreshToken);
        Assert.Equal(sandboxDto.Email, userEmail);
    }

    [Fact]
    public async Task Validation_InvalidUserDto_ShouldReturnBadRequest400WithErrors()
    {
        var client = _factory.CreateClient();

        // Створюємо DTO з некоректним email та порожнім ім'ям (порушення DataAnnotations)
        var invalidUser = new CreateUserDto
        {
            FirstName = "", // Обов'язкове поле!
            LastName = "Тестов",
            Email = "not-an-email", // Помилка формату!
            PhoneNumber = "123",
            Password = "" // Обов'язкове поле!
        };

        var response = await client.PostAsJsonAsync("/api/users", invalidUser);

        // Очікуємо 400 Bad Request від автоматичної валідації моделі (Етап 7)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Перевіряємо RFC 7807 структуру ProblemDetails
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors) || 
                    doc.RootElement.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task SecurityStatus_ShouldConfirmSecretsAndStagesConfigured()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/system/security-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Secured", status);

        var stages = doc.RootElement.GetProperty("stages");
        Assert.True(stages.TryGetProperty("stage5_jwt", out _));
        Assert.True(stages.TryGetProperty("stage6_authorization_and_security", out _));
        Assert.True(stages.TryGetProperty("stage7_oauth_secrets_validation", out _));
    }
}
