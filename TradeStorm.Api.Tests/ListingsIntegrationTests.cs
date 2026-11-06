using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using Xunit;

namespace TradeStorm.Api.Tests;

public class ListingsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ListingsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateListing_And_ToggleFavorite_ModifiesDatabaseCorrectly()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"seller_{Guid.NewGuid():N}@loopmarket.com";

        // 1. Реєструємо та логінимо користувача
        var regDto = new CreateUserDto
        {
            FirstName = "Тарас",
            LastName = "Шевченко",
            Email = uniqueEmail,
            Password = "Password123!",
            PhoneNumber = $"38095{Random.Shared.Next(1000000, 9999999)}"
        };
        await client.PostAsJsonAsync("/api/users", regDto);

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto { Email = uniqueEmail, Password = "Password123!" });
        var loginDoc = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Створюємо нове оголошення
        var listingDto = new CreateListingDto
        {
            Title = "Підручник з клієнт-серверних застосунків",
            Description = "Повний курс ASP.NET Core для 7 семестру КГТА",
            Price = 250.0m,
            CategoryId = 3, // Навчальні матеріали та книги
            Condition = "Нове",
            Location = "Кременчук, КГТА",
            IsShippingAvailable = true
        };

        var createRes = await client.PostAsJsonAsync("/api/listings", listingDto);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var createdDoc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        int createdId = createdDoc.RootElement.GetProperty("id").GetInt32();
        Assert.True(createdId > 0);

        // 3. Перевіряємо стан бази даних (Запит -> Відповідь -> Стан бази даних)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbListing = await db.Listings.FindAsync(createdId);
            Assert.NotNull(dbListing);
            Assert.Equal("Підручник з клієнт-серверних застосунків", dbListing.Title);
            Assert.Equal(250.0m, dbListing.Price);
        }

        // 4. Додаємо оголошення до обраного
        var favRes = await client.PostAsync($"/api/favorites/{createdId}", null);
        Assert.Equal(HttpStatusCode.OK, favRes.StatusCode);

        // 5. Перевіряємо стан обраного в базі даних
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbFav = await db.Favorites.FindAsync(1);
            Assert.NotNull(dbFav);
            Assert.Equal(createdId, dbFav.ListingId);
        }
    }
}
