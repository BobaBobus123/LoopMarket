using System;
using System.Net;
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

public class OrdersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullOrderLifecycle_BuyerCreates_SellerShips_BuyerCompletes_WorksCorrectly()
    {
        var client = _factory.CreateClient();

        // 1. Створюємо Продавця (Seller)
        var sellerEmail = $"seller_{Guid.NewGuid():N}@kgta.edu.ua";
        await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Олександр",
            LastName = "Продавець",
            Email = sellerEmail,
            Password = "Password123!",
            PhoneNumber = $"38096{Random.Shared.Next(1000000, 9999999)}"
        });

        var sellerLogin = await client.PostAsJsonAsync("/api/users/login", new LoginDto { Email = sellerEmail, Password = "Password123!" });
        var sellerToken = JsonDocument.Parse(await sellerLogin.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();

        // 2. Продавець створює оголошення
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        var createListingRes = await client.PostAsJsonAsync("/api/listings", new CreateListingDto
        {
            Title = "Ноутбук Dell XPS для програмування",
            Description = "Ідеальний стан, 16GB RAM, SSD 512GB",
            Price = 18500.0m,
            CategoryId = 1, // Електроніка
            Condition = "Вживане",
            Location = "Кременчук, Центр",
            IsShippingAvailable = true
        });
        Assert.Equal(HttpStatusCode.Created, createListingRes.StatusCode);
        var listingDoc = JsonDocument.Parse(await createListingRes.Content.ReadAsStringAsync());
        int listingId = listingDoc.RootElement.GetProperty("id").GetInt32();

        // 3. Створюємо Покупця (Buyer)
        var buyerEmail = $"buyer_{Guid.NewGuid():N}@kgta.edu.ua";
        await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Михайло",
            LastName = "Покупець",
            Email = buyerEmail,
            Password = "Password123!",
            PhoneNumber = $"38097{Random.Shared.Next(1000000, 9999999)}"
        });

        var buyerLogin = await client.PostAsJsonAsync("/api/users/login", new LoginDto { Email = buyerEmail, Password = "Password123!" });
        var buyerToken = JsonDocument.Parse(await buyerLogin.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();

        // 4. Покупець оформлює замовлення
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        var orderRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderDto
        {
            ListingId = listingId,
            BuyerName = "Михайло Покупець",
            BuyerPhone = "+380971234567",
            DeliveryCity = "Кременчук",
            DeliveryBranch = "Відділення № 4",
            PaymentMethod = "Накладений платіж",
            Comment = "Термінова доставка"
        });
        Assert.Equal(HttpStatusCode.Created, orderRes.StatusCode);
        var orderDoc = JsonDocument.Parse(await orderRes.Content.ReadAsStringAsync());
        int orderId = orderDoc.RootElement.GetProperty("id").GetInt32();
        Assert.Equal("Pending", orderDoc.RootElement.GetProperty("status").GetString());

        // 5. Продавець перевіряє свої продажі (my-sales) та відправляє замовлення з ТТН
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        var salesRes = await client.GetAsync("/api/orders/my-sales");
        Assert.Equal(HttpStatusCode.OK, salesRes.StatusCode);

        // Продавець позначає як Відправлено
        var shipRes = await client.PatchAsJsonAsync($"/api/orders/{orderId}/status", new UpdateOrderStatusDto
        {
            Status = "Shipped",
            TrackingNumber = "20450912345678"
        });
        Assert.Equal(HttpStatusCode.OK, shipRes.StatusCode);
        var shippedDoc = JsonDocument.Parse(await shipRes.Content.ReadAsStringAsync());
        Assert.Equal("Shipped", shippedDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal("20450912345678", shippedDoc.RootElement.GetProperty("trackingNumber").GetString());

        // 6. Покупець перевіряє свої покупки (my-purchases) та підтверджує отримання
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        var purchasesRes = await client.GetAsync("/api/orders/my-purchases");
        Assert.Equal(HttpStatusCode.OK, purchasesRes.StatusCode);

        var completeRes = await client.PatchAsJsonAsync($"/api/orders/{orderId}/status", new UpdateOrderStatusDto
        {
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);

        // 7. Перевірка бази даних: товар отримав статус 'Sold', замовлення 'Completed'
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dbOrder = await db.Orders.FindAsync(orderId);
        Assert.NotNull(dbOrder);
        Assert.Equal("Completed", dbOrder.Status);
        Assert.Equal("20450912345678", dbOrder.TrackingNumber);

        var dbListing = await db.Listings.FindAsync(listingId);
        Assert.NotNull(dbListing);
        Assert.Equal("Sold", dbListing.Status);
    }
}
