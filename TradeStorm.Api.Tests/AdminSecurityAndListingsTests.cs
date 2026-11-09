using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using Xunit;

namespace TradeStorm.Api.Tests;

public class AdminSecurityAndListingsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminSecurityAndListingsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string Token, User User)> CreateUserWithRoleAsync(string role = UserRoles.Buyer)
    {
        var client = _factory.CreateClient();
        var email = $"user_{Guid.NewGuid():N}@loopmarket.com";
        var password = "SecurePassword123!";
        var phone = $"38097{Random.Shared.Next(1000000, 9999999)}";

        var regDto = new CreateUserDto
        {
            FirstName = "Тест",
            LastName = "Користувач",
            Email = email,
            Password = password,
            PhoneNumber = phone
        };

        var regRes = await client.PostAsJsonAsync("/api/users", regDto);
        regRes.EnsureSuccessStatusCode();

        var regJson = await regRes.Content.ReadAsStringAsync();
        using var regDoc = JsonDocument.Parse(regJson);
        int createdId = regDoc.RootElement.GetProperty("id").GetInt32();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(createdId);
            if (u != null)
            {
                u.Role = role;
                await db.SaveChangesAsync();
            }
        }

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = email,
            Password = password
        });
        loginRes.EnsureSuccessStatusCode();

        var loginJson = await loginRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginJson);
        var token = doc.RootElement.GetProperty("token").GetString()!;

        var userObj = new User
        {
            Id = createdId,
            Email = email,
            FirstName = "Тест",
            LastName = "Користувач",
            Role = role
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token, userObj);
    }

    [Fact]
    public async Task AdminBanAndUnbanUser_ShouldReturn200Success()
    {
        var (adminClient, _, _) = await CreateUserWithRoleAsync(UserRoles.Admin);
        var (_, _, targetUser) = await CreateUserWithRoleAsync(UserRoles.Buyer);

        // 1. Блокуємо користувача
        var banRes = await adminClient.PatchAsJsonAsync($"/api/users/admin/{targetUser.Id}/ban", new BanUserDto
        {
            IsBanned = true,
            Reason = "Тестове порушення"
        });
        Assert.Equal(HttpStatusCode.OK, banRes.StatusCode);

        var banJson = await banRes.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(banJson))
        {
            Assert.True(doc.RootElement.GetProperty("isBanned").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        }

        // 2. Розблоковуємо користувача
        var unbanRes = await adminClient.PatchAsJsonAsync($"/api/users/admin/{targetUser.Id}/ban", new BanUserDto
        {
            IsBanned = false
        });
        Assert.Equal(HttpStatusCode.OK, unbanRes.StatusCode);

        var unbanJson = await unbanRes.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(unbanJson))
        {
            Assert.False(doc.RootElement.GetProperty("isBanned").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        }
    }

    [Fact]
    public async Task MainAdmin_CannotBeBannedOrDeleted()
    {
        var (adminClient, _, _) = await CreateUserWithRoleAsync(UserRoles.Admin);

        // Створюємо або оновлюємо головного адміна Нікіту
        int mainAdminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingNikita = await db.Users.FindAsync(1);
            if (existingNikita == null)
            {
                var nikita = new User
                {
                    Id = 1,
                    Email = "nikitazhidenko@gmail.com",
                    PasswordHash = "hash",
                    FirstName = "Нікіта",
                    LastName = "Адмін",
                    Role = UserRoles.Admin
                };
                db.Users.Add(nikita);
                await db.SaveChangesAsync();
                mainAdminId = 1;
            }
            else
            {
                existingNikita.Email = "nikitazhidenko@gmail.com";
                existingNikita.Role = UserRoles.Admin;
                await db.SaveChangesAsync();
                mainAdminId = existingNikita.Id;
            }
        }

        // Спроба заблокувати Нікіту повинна повертати 400 Bad Request
        var banNikitaRes = await adminClient.PatchAsJsonAsync($"/api/users/admin/{mainAdminId}/ban", new BanUserDto
        {
            IsBanned = true,
            Reason = "Неможливо заблокувати"
        });
        Assert.Equal(HttpStatusCode.BadRequest, banNikitaRes.StatusCode);

        // Спроба видалити Нікіту повинна повертати 400 Bad Request
        var delNikitaRes = await adminClient.DeleteAsync($"/api/users/admin/{mainAdminId}");
        Assert.Equal(HttpStatusCode.BadRequest, delNikitaRes.StatusCode);
    }

    [Fact]
    public async Task GetUserListingsForAdmin_ShouldReturnListingsArray()
    {
        var (adminClient, _, _) = await CreateUserWithRoleAsync(UserRoles.Admin);
        var (sellerClient, _, sellerUser) = await CreateUserWithRoleAsync(UserRoles.Seller);

        // Створюємо оголошення для продавця
        var createListingDto = new CreateListingDto
        {
            Title = "Ноутбук Apple MacBook Air M1",
            Description = "В ідеальному стані",
            Price = 25000,
            CategoryId = 1,
            Condition = "Вживане",
            IsShippingAvailable = true
        };
        var createRes = await sellerClient.PostAsJsonAsync("/api/listings", createListingDto);
        createRes.EnsureSuccessStatusCode();

        // Адмін переглядає оголошення цього продавця
        var res = await adminClient.GetAsync($"/api/users/admin/{sellerUser.Id}/listings");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(sellerUser.Id, doc.RootElement.GetProperty("userId").GetInt32());
        Assert.True(doc.RootElement.GetProperty("totalListings").GetInt32() >= 1);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("listings").ValueKind);
    }
}
