using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using Xunit;

namespace TradeStorm.Api.Tests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterAndLogin_ShouldReturnAccessAndRefreshToken()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"user_{Guid.NewGuid():N}@loopmarket.com";
        var uniquePhone = $"38099{Random.Shared.Next(1000000, 9999999)}";

        // 1. Реєстрація нового користувача
        var registerDto = new CreateUserDto
        {
            FirstName = "Іван",
            LastName = "Франко",
            Email = uniqueEmail,
            Password = "SecurePassword123!",
            PhoneNumber = uniquePhone
        };

        var regResponse = await client.PostAsJsonAsync("/api/users", registerDto);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // 2. Вхід користувача (отримання Access + Refresh Token)
        var loginDto = new LoginDto
        {
            Email = uniqueEmail,
            Password = "SecurePassword123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/users/login", loginDto);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        using var loginDoc = JsonDocument.Parse(loginJson);
        var accessToken = loginDoc.RootElement.GetProperty("token").GetString();
        var refreshToken = loginDoc.RootElement.GetProperty("refreshToken").GetString();

        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);

        // 3. Оновлення пари токенів через Refresh Token (Етап 5)
        var refreshDto = new TokenRefreshDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };

        var refreshResponse = await client.PostAsJsonAsync("/api/users/refresh", refreshDto);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
        using var refreshDoc = JsonDocument.Parse(refreshJson);
        var newAccessToken = refreshDoc.RootElement.GetProperty("token").GetString();
        var newRefreshToken = refreshDoc.RootElement.GetProperty("refreshToken").GetString();

        Assert.NotNull(newAccessToken);
        Assert.NotNull(newRefreshToken);
        Assert.NotEqual(refreshToken, newRefreshToken); // Ротація успішна!

        // 4. Завершення сесії (Revoke / Logout)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        var revokeResponse = await client.PostAsync("/api/users/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }
}
