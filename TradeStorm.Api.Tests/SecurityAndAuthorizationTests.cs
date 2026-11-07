using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using Xunit;

namespace TradeStorm.Api.Tests;

/// <summary>
/// Інтеграційні тести для Етапу 6: «Авторизація та захист від типових атак»
/// </summary>
public class SecurityAndAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityAndAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Response_ShouldContainOWASPSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Перевірка наявності обов'язкових OWASP Security Headers (Етап 6)
        Assert.True(response.Headers.Contains("X-Content-Type-Options"), "Відсутній заголовок X-Content-Type-Options");
        Assert.True(response.Headers.Contains("X-Frame-Options"), "Відсутній заголовок X-Frame-Options");
        Assert.True(response.Headers.Contains("Referrer-Policy"), "Відсутній заголовок Referrer-Policy");
        Assert.True(response.Headers.Contains("X-XSS-Protection"), "Відсутній заголовок X-XSS-Protection");
    }

    [Fact]
    public async Task UnauthenticatedUser_AccessingProtectedEndpoint_ShouldReturnUnauthorized401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RBAC_BuyerCannotAccessAdminEndpoint_ShouldReturnForbidden403()
    {
        var client = _factory.CreateClient();

        // 1. Реєструємо звичайного користувача (Buyer)
        var buyerEmail = $"buyer_{Guid.NewGuid():N}@loopmarket.com";
        var buyerPhone = $"38095{Random.Shared.Next(1000000, 9999999)}";
        var regDto = new CreateUserDto
        {
            FirstName = "Покупець",
            LastName = "Тестовий",
            Email = buyerEmail,
            Password = "Password123!",
            PhoneNumber = buyerPhone
        };
        var regRes = await client.PostAsJsonAsync("/api/users", regDto);
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        // 2. Логінимося як Buyer
        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = buyerEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginJson = await loginRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginJson);
        var token = doc.RootElement.GetProperty("token").GetString();

        // 3. Спроба виконати дію адміністратора (перегляд списку користувачів адмін-панелі)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var adminRes = await client.GetAsync("/api/users/admin/all");

        // RBAC: роль Buyer повинна отримувати 403 Forbidden!
        Assert.Equal(HttpStatusCode.Forbidden, adminRes.StatusCode);
    }

    [Fact]
    public async Task AdminApi_UnauthenticatedVisitor_ShouldReturnUnauthorized401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotFoundPage_ShouldBeAccessible()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/404.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
