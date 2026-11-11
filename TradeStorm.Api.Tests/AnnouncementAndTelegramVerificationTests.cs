using System;
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
using Xunit;

namespace TradeStorm.Api.Tests;

public class AnnouncementAndTelegramVerificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnnouncementAndTelegramVerificationTests(CustomWebApplicationFactory factory)
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
                u.IsPhoneVerified = true;
                u.TelegramChatId = 123456789;
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
            Role = role,
            IsPhoneVerified = true,
            TelegramChatId = 123456789
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token, userObj);
    }

    [Fact]
    public async Task CreateListing_WithoutVerification_Returns403Forbidden()
    {
        // Arrange: User with token, but neither Telegram nor Email verified and non-test domain
        var (client, _, user) = await CreateUserWithRoleAsync(UserRoles.Buyer);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(user.Id);
            if (u != null)
            {
                u.Email = $"realuser_{Guid.NewGuid():N}@gmail.com";
                u.IsPhoneVerified = false;
                u.IsEmailVerified = false;
                u.TelegramChatId = 0;
                await db.SaveChangesAsync();
            }
        }

        var listingDto = new CreateListingDto
        {
            Title = "Ноутбук Apple MacBook Air M1",
            Description = "Чудовий стан, оригінальний комплект, батарея 95%",
            Price = 28000,
            CategoryId = 1,
            Location = "Київ",
            ImageUrls = new System.Collections.Generic.List<string> { "/uploads/test-mac.jpg" },
            Condition = "Нове"
        };

        // Act
        var res = await client.PostAsJsonAsync("/api/listings", listingDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("VERIFICATION_REQUIRED", body);
    }

    [Fact]
    public async Task User_CanVerifyEmailWithCode_AndPublishListing()
    {
        // Arrange: User with unverified status
        var (client, _, user) = await CreateUserWithRoleAsync(UserRoles.Buyer);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(user.Id);
            if (u != null)
            {
                u.Email = $"buyer_{Guid.NewGuid():N}@gmail.com";
                u.IsPhoneVerified = false;
                u.IsEmailVerified = false;
                u.TelegramChatId = 0;
                await db.SaveChangesAsync();
            }
        }

        // 1. Request Email verification code
        var sendRes = await client.PostAsync("/api/users/send-email-code", null);
        Assert.Equal(HttpStatusCode.OK, sendRes.StatusCode);

        string? generatedCode = null;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(user.Id);
            generatedCode = u?.EmailVerificationCode;
        }

        Assert.NotNull(generatedCode);
        Assert.Equal(6, generatedCode!.Length);

        // 2. Submit wrong code -> should fail
        var wrongRes = await client.PostAsJsonAsync("/api/users/verify-email-code", new VerifyEmailCodeDto { Code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongRes.StatusCode);

        // 3. Submit correct code -> should succeed
        var verifyRes = await client.PostAsJsonAsync("/api/users/verify-email-code", new VerifyEmailCodeDto { Code = generatedCode });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // 4. Now user can publish listing without Telegram!
        var listingDto = new CreateListingDto
        {
            Title = "Смартфон Samsung Galaxy S23 Ultra",
            Description = "Офіційний, на гарантії, повний комплект",
            Price = 32000,
            CategoryId = 1,
            Location = "Львів",
            ImageUrls = new System.Collections.Generic.List<string> { "/uploads/test-s23.jpg" },
            Condition = "Нове"
        };

        var pubRes = await client.PostAsJsonAsync("/api/listings", listingDto);
        Assert.Equal(HttpStatusCode.Created, pubRes.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateAnnouncement_AndQueryByPageAndPlacement()
    {
        // Arrange
        var (adminClient, _, _) = await CreateUserWithRoleAsync(UserRoles.Admin);
        var pubClient = _factory.CreateClient();

        var dto = new SiteAnnouncementDto
        {
            Title = "Тут могла бути ваша реклама!",
            Message = "Залучайте тисячі покупців щодня на LoopMarket.",
            Type = "promo",
            TargetPage = "home",
            Placement = "catalog",
            Duration = "7d",
            ActionText = "Розмістити рекламу",
            ActionUrl = "https://t.me/LoopMarketSupport",
            IsActive = true
        };

        // Act: Create announcement as Admin
        var postRes = await adminClient.PostAsJsonAsync("/api/admin/announcement", dto);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        var postJson = await postRes.Content.ReadAsStringAsync();
        using var postDoc = JsonDocument.Parse(postJson);
        int createdId = postDoc.RootElement.GetProperty("id").GetInt32();

        // Act: Query active announcements publicly for home catalog
        var activeRes = await pubClient.GetAsync("/api/admin/announcement/active?page=home&placement=catalog");
        Assert.Equal(HttpStatusCode.OK, activeRes.StatusCode);

        var activeJson = await activeRes.Content.ReadAsStringAsync();
        using var activeDoc = JsonDocument.Parse(activeJson);
        Assert.True(activeDoc.RootElement.GetProperty("hasActive").GetBoolean());
        Assert.Equal("promo", activeDoc.RootElement.GetProperty("type").GetString());

        // Act: Query active announcements for a different page ("services")
        var diffPageRes = await pubClient.GetAsync("/api/admin/announcement/active?page=services&placement=catalog");
        Assert.Equal(HttpStatusCode.OK, diffPageRes.StatusCode);
        using var diffDoc = JsonDocument.Parse(await diffPageRes.Content.ReadAsStringAsync());
        Assert.False(diffDoc.RootElement.GetProperty("hasActive").GetBoolean());

        // Act: Deactivate announcement as Admin
        var delRes = await adminClient.DeleteAsync($"/api/admin/announcement/{createdId}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        // Verify deactivated
        var checkRes = await pubClient.GetAsync("/api/admin/announcement/active?page=home&placement=catalog");
        using var checkDoc = JsonDocument.Parse(await checkRes.Content.ReadAsStringAsync());
        Assert.False(checkDoc.RootElement.GetProperty("hasActive").GetBoolean());
    }

    [Fact]
    public async Task Admin_CanCreateSidebarAdBannerWithImageOnly_AndInfoBannerWithoutTitle()
    {
        var (adminClient, _, _) = await CreateUserWithRoleAsync(UserRoles.Admin);
        var pubClient = _factory.CreateClient();

        // 1. Image-only sidebar ad banner (empty Title and empty Message)
        var adDto = new SiteAnnouncementDto
        {
            Title = "",
            Message = "",
            ImageUrl = "/uploads/test-sidebar-ad.png",
            Type = "promo",
            TargetPage = "all",
            Placement = "sidebar_both",
            Duration = "7d",
            ActionText = "Детальніше",
            ActionUrl = "https://t.me/LoopMarketSupport",
            IsActive = true
        };

        var adRes = await adminClient.PostAsJsonAsync("/api/admin/announcement", adDto);
        Assert.Equal(HttpStatusCode.OK, adRes.StatusCode);
        using var adDoc = JsonDocument.Parse(await adRes.Content.ReadAsStringAsync());
        int adId = adDoc.RootElement.GetProperty("id").GetInt32();

        // 2. Info banner with optional Title left empty (as in admin.html form)
        var infoDto = new SiteAnnouncementDto
        {
            Title = "",
            Message = "Планове оновлення платформи сьогодні вночі.",
            Type = "warning",
            TargetPage = "all",
            Placement = "top",
            Duration = "24h",
            ActionText = null,
            ActionUrl = null,
            IsActive = true
        };

        var infoRes = await adminClient.PostAsJsonAsync("/api/admin/announcement", infoDto);
        Assert.Equal(HttpStatusCode.OK, infoRes.StatusCode);
        using var infoDoc = JsonDocument.Parse(await infoRes.Content.ReadAsStringAsync());
        int infoId = infoDoc.RootElement.GetProperty("id").GetInt32();

        // 3. List all announcements as Admin
        var listRes = await adminClient.GetAsync("/api/admin/announcements");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        using var listDoc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
        Assert.True(listDoc.RootElement.GetArrayLength() >= 2);

        // 4. Both adBanner and infoBanner are returned actively for details page
        var activeRes = await pubClient.GetAsync("/api/admin/announcement/active?page=details");
        Assert.Equal(HttpStatusCode.OK, activeRes.StatusCode);
        using var activeDoc = JsonDocument.Parse(await activeRes.Content.ReadAsStringAsync());
        Assert.True(activeDoc.RootElement.GetProperty("hasActive").GetBoolean());

        var activeAd = activeDoc.RootElement.GetProperty("adBanner");
        Assert.Equal(adId, activeAd.GetProperty("id").GetInt32());
        Assert.Equal("sidebar_both", activeAd.GetProperty("placement").GetString());
        Assert.Equal("/uploads/test-sidebar-ad.png", activeAd.GetProperty("imageUrl").GetString());

        var activeInfo = activeDoc.RootElement.GetProperty("infoBanner");
        Assert.Equal(infoId, activeInfo.GetProperty("id").GetInt32());
        Assert.Equal("top", activeInfo.GetProperty("placement").GetString());

        // Cleanup both
        await adminClient.DeleteAsync($"/api/admin/announcement/{adId}");
        await adminClient.DeleteAsync($"/api/admin/announcement/{infoId}");
    }

    [Fact]
    public async Task DatabaseSchemaInitializer_CreatesMissingTable_AndRepairsLegacyNullColumns()
    {
        // Case A: Existing database missing SiteAnnouncements completely (e.g. EnsureCreated skipped)
        var optionsMissing = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using (var dbMissing = new AppDbContext(optionsMissing))
        {
            await dbMissing.Database.OpenConnectionAsync();
            DatabaseSchemaInitializer.EnsureMissingTablesAndColumns(dbMissing, isPostgres: false);

            dbMissing.SiteAnnouncements.Add(new SiteAnnouncement
            {
                Title = "Банер після ініціалізації",
                Message = "Перевірка створення таблиці",
                Type = "promo",
                TargetPage = "all",
                Placement = "sidebar_both",
                IsActive = true
            });
            await dbMissing.SaveChangesAsync();

            var count = await dbMissing.SiteAnnouncements.CountAsync();
            Assert.Equal(1, count);
        }

        // Case B: Legacy SiteAnnouncements schema missing new columns and containing NULL values
        var optionsLegacy = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using (var dbLegacy = new AppDbContext(optionsLegacy))
        {
            await dbLegacy.Database.OpenConnectionAsync();
            await dbLegacy.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE SiteAnnouncements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Message TEXT NULL,
                    Type TEXT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL
                );
                INSERT INTO SiteAnnouncements (Message, Type, IsActive, CreatedAt)
                VALUES (NULL, NULL, 1, '2026-01-01T00:00:00Z');");

            DatabaseSchemaInitializer.EnsureMissingTablesAndColumns(dbLegacy, isPostgres: false);

            var rows = await dbLegacy.SiteAnnouncements.ToListAsync();
            Assert.Single(rows);
            Assert.Equal(string.Empty, rows[0].Title);
            Assert.Equal("all", rows[0].TargetPage);
            Assert.Equal("top", rows[0].Placement);
            Assert.Equal("warning", rows[0].Type);
        }
    }
}
