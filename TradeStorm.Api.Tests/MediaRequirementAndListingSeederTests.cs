using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;
using Xunit;

namespace TradeStorm.Api.Tests;

public class MediaRequirementAndListingSeederTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MediaRequirementAndListingSeederTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, int UserId)> CreateAuthenticatedUserAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"mtest_{role.ToLower()}_{Guid.NewGuid():N}@loopmarket.com";
        var phone = $"+38095{Random.Shared.Next(1000000, 9999999)}";

        var regRes = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Tester",
            LastName = role,
            Email = email,
            Password = "Password123!",
            PhoneNumber = phone
        });
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);
        var regJson = await regRes.Content.ReadFromJsonAsync<JsonElement>();
        int userId = regJson.GetProperty("id").GetInt32();

        if (role == UserRoles.Admin)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(userId);
            Assert.NotNull(u);
            u!.Role = UserRoles.Admin;
            u.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = email,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        string token = loginJson.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, userId);
    }

    [Fact]
    public async Task CreateListing_WithoutPhotoOrVideo_ShouldReturnBadRequest_ForGoodsAndServices()
    {
        var (client, _) = await CreateAuthenticatedUserAsync(UserRoles.Buyer);

        // 1. No media at all
        var noMediaRes = await client.PostAsJsonAsync("/api/listings", new CreateListingDto
        {
            Title = "Товар без фото",
            Description = "Тестовий опис товару без жодного медіафайлу",
            Price = 500,
            CategoryId = 1,
            Condition = "Нове"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noMediaRes.StatusCode);
        var noMediaBody = await noMediaRes.Content.ReadAsStringAsync();
        Assert.Contains("MEDIA_REQUIRED", noMediaBody);

        // 2. Placeholder SVG URL should NOT satisfy media requirement
        var placeholderRes = await client.PostAsJsonAsync("/api/listings", new CreateListingDto
        {
            Title = "Товар із заглушкою",
            Description = "Перевірка заборони заглушок",
            Price = 500,
            CategoryId = 1,
            ImageUrls = new List<string> { "/images/placeholder.svg" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, placeholderRes.StatusCode);
        Assert.Contains("MEDIA_REQUIRED", await placeholderRes.Content.ReadAsStringAsync());

        // 3. Service listing without media should ALSO be rejected
        var serviceNoMediaRes = await client.PostAsJsonAsync("/api/listings", new CreateListingDto
        {
            Title = "Послуга репетитора без фото",
            Description = "Послуга без фото і відео",
            Price = 400,
            CategoryId = 8,
            Condition = "Погодинно • Онлайн"
        });
        Assert.Equal(HttpStatusCode.BadRequest, serviceNoMediaRes.StatusCode);
        Assert.Contains("MEDIA_REQUIRED", await serviceNoMediaRes.Content.ReadAsStringAsync());

        // 4. Valid photo -> 201 Created
        var validPhotoRes = await client.PostAsJsonAsync("/api/listings", new CreateListingDto
        {
            Title = "Товар з реальним фото",
            Description = "Все добре, фото додано",
            Price = 1200,
            CategoryId = 1,
            ImageUrls = new List<string> { "/uploads/valid-photo.jpg" }
        });
        Assert.Equal(HttpStatusCode.Created, validPhotoRes.StatusCode);
        var createdJson = await validPhotoRes.Content.ReadFromJsonAsync<JsonElement>();
        int createdId = createdJson.GetProperty("id").GetInt32();

        // 5. Removing all photos via PUT without adding video -> 400 BadRequest
        var removeMediaRes = await client.PutAsJsonAsync($"/api/listings/{ListingIdEncoder.Encode(createdId)}", new UpdateListingDto
        {
            Title = "Товар з реальним фото",
            Description = "Опис без фото",
            Price = 1200,
            CategoryId = 1,
            ImageUrls = new List<string>(),
            VideoUrl = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, removeMediaRes.StatusCode);
        Assert.Contains("MEDIA_REQUIRED", await removeMediaRes.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdminSeedTemplates_ShouldCreateDraftProductsWithoutServices_AndPublishOnMediaAdd()
    {
        var (adminClient, adminId) = await CreateAuthenticatedUserAsync(UserRoles.Admin);
        var anonClient = _factory.CreateClient();

        // 1. Seed draft product templates
        var seedRes = await adminClient.PostAsync("/api/listings/admin/seed-templates", null);
        Assert.Equal(HttpStatusCode.OK, seedRes.StatusCode);
        var seedJson = await seedRes.Content.ReadFromJsonAsync<JsonElement>();
        int createdCount = seedJson.GetProperty("createdCount").GetInt32();
        Assert.Equal(30, createdCount);

        // 2. Re-running should be idempotent
        var secondSeedRes = await adminClient.PostAsync("/api/listings/admin/seed-templates", null);
        Assert.Equal(HttpStatusCode.OK, secondSeedRes.StatusCode);
        var secondSeedJson = await secondSeedRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondSeedJson.GetProperty("createdCount").GetInt32());

        // 3. Verify DB drafts: all belong to admin, Status == Draft, non-empty descriptions, NO services
        Listing sampleDraft;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var drafts = await db.Listings
                .Include(l => l.Category)
                    .ThenInclude(c => c!.Parent)
                .Where(l => l.SellerId == adminId && l.Status == "Draft")
                .ToListAsync();

            Assert.Equal(30, drafts.Count);
            Assert.All(drafts, d => Assert.False(string.IsNullOrWhiteSpace(d.Description)));

            var serviceDrafts = drafts.Where(d =>
                (d.Category != null && d.Category.Name.Contains("Послуги", StringComparison.OrdinalIgnoreCase)) ||
                (d.Category?.Parent != null && d.Category.Parent.Name.Contains("Послуги", StringComparison.OrdinalIgnoreCase)) ||
                d.Title.Contains("Послуг", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Empty(serviceDrafts);

            sampleDraft = drafts[0];
        }

        string sampleCode = ListingIdEncoder.Encode(sampleDraft.Id);

        // 4. Drafts must be hidden from anonymous public catalog & direct lookup
        var anonCatalogRes = await anonClient.GetAsync("/api/listings?pageSize=200");
        Assert.Equal(HttpStatusCode.OK, anonCatalogRes.StatusCode);
        var anonCatalogJson = await anonCatalogRes.Content.ReadFromJsonAsync<JsonElement>();
        var anonIds = anonCatalogJson.GetProperty("listings").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        Assert.DoesNotContain(sampleDraft.Id, anonIds);

        var anonStatusDraftRes = await anonClient.GetAsync("/api/listings?status=Draft");
        Assert.Equal(HttpStatusCode.OK, anonStatusDraftRes.StatusCode);
        var anonStatusDraftJson = await anonStatusDraftRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, anonStatusDraftJson.GetProperty("listings").GetArrayLength());

        var anonDirectRes = await anonClient.GetAsync($"/api/listings/{sampleCode}");
        Assert.Equal(HttpStatusCode.NotFound, anonDirectRes.StatusCode);

        // 5. Admin / owner CAN see their drafts with includeDrafts=true & direct GET
        var adminDraftDirect = await adminClient.GetAsync($"/api/listings/{sampleCode}");
        Assert.Equal(HttpStatusCode.OK, adminDraftDirect.StatusCode);

        var adminOwnWithDrafts = await adminClient.GetAsync($"/api/listings?sellerId={adminId}&pageSize=100&includeDrafts=true");
        Assert.Equal(HttpStatusCode.OK, adminOwnWithDrafts.StatusCode);
        var adminOwnJson = await adminOwnWithDrafts.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(sampleDraft.Id, adminOwnJson.GetProperty("listings").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));

        // 6. Publishing Draft WITHOUT adding photo/video via PATCH /status must fail
        var failPublishRes = await adminClient.PatchAsJsonAsync($"/api/listings/{sampleCode}/status", new { status = "Active" });
        Assert.Equal(HttpStatusCode.BadRequest, failPublishRes.StatusCode);
        Assert.Contains("MEDIA_REQUIRED", await failPublishRes.Content.ReadAsStringAsync());

        // 7. Adding real photo via PUT updates Draft -> Active and makes it visible publicly
        var addMediaRes = await adminClient.PutAsJsonAsync($"/api/listings/{sampleCode}", new UpdateListingDto
        {
            Title = sampleDraft.Title,
            Description = sampleDraft.Description,
            Price = sampleDraft.Price,
            CategoryId = sampleDraft.CategoryId,
            Condition = sampleDraft.Condition,
            Location = sampleDraft.Location,
            IsShippingAvailable = sampleDraft.IsShippingAvailable,
            ImageUrls = new List<string> { "/uploads/sample-added-photo.jpg" }
        });
        Assert.Equal(HttpStatusCode.OK, addMediaRes.StatusCode);
        var addMediaJson = await addMediaRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", addMediaJson.GetProperty("status").GetString());

        var anonAfterRes = await anonClient.GetAsync($"/api/listings/{sampleCode}");
        Assert.Equal(HttpStatusCode.OK, anonAfterRes.StatusCode);
    }
}
