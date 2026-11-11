using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TradeStorm.Api.Controllers;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;
using Xunit;
using TgChat = Telegram.Bot.Types.Chat;
using TgMessage = Telegram.Bot.Types.Message;
using TgUser = Telegram.Bot.Types.User;

namespace TradeStorm.Api.Tests;

public class BotWebhookAndHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BotWebhookAndHardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public class SentTelegramPayload
    {
        public string? Text { get; set; }
        public object? ReplyMarkup { get; set; }
    }

    public class RecordingTelegramClientProxy : DispatchProxy
    {
        public List<SentTelegramPayload> SentMessages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;

            if (args != null && args.Length > 0 && args[0] != null)
            {
                var req = args[0]!;
                var textProp = req.GetType().GetProperty("Text");
                if (textProp != null)
                {
                    var textVal = textProp.GetValue(req)?.ToString();
                    var markupVal = req.GetType().GetProperty("ReplyMarkup")?.GetValue(req);
                    SentMessages.Add(new SentTelegramPayload
                    {
                        Text = textVal,
                        ReplyMarkup = markupVal
                    });
                }
            }

            var returnType = targetMethod.ReturnType;
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = returnType.GetGenericArguments()[0];
                object? innerValue = null;
                if (innerType == typeof(TgMessage))
                {
                    innerValue = new TgMessage();
                }
                else if (innerType.IsValueType)
                {
                    innerValue = Activator.CreateInstance(innerType);
                }
                else if (innerType.GetConstructor(Type.EmptyTypes) != null)
                {
                    innerValue = Activator.CreateInstance(innerType);
                }

                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(innerType)
                    .Invoke(null, new[] { innerValue });
            }

            if (returnType == typeof(TimeSpan)) return TimeSpan.FromSeconds(10);
            if (returnType == typeof(long)) return 999999L;
            if (returnType == typeof(bool)) return false;

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }

    private async Task<(HttpClient Client, int UserId, string Email)> CreateVerifiedUserAsync(string role = UserRoles.Seller)
    {
        var client = _factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@loopmarket.com";
        var phone = $"38097{Random.Shared.Next(1000000, 9999999)}";

        var reg = new CreateUserDto
        {
            FirstName = "Тест",
            LastName = "Користувач",
            Email = email,
            Password = "StrongPassword123!",
            PhoneNumber = phone
        };

        var regRes = await client.PostAsJsonAsync("/api/users", reg);
        regRes.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        user.Role = role;
        user.IsPhoneVerified = true;
        user.IsEmailVerified = true;
        await db.SaveChangesAsync();

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = email,
            Password = "StrongPassword123!"
        });
        loginRes.EnsureSuccessStatusCode();

        using var loginDoc = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, user.Id, email);
    }

    [Fact]
    public async Task TelegramBotService_StandaloneStart_SendsSetupGuide_AndRecognizesLinkedUser()
    {
        var botService = _factory.Services.GetRequiredService<TelegramBotService>();

        // 1. Unlinked user opens bot directly via /start
        var unlinkedClient = DispatchProxy.Create<ITelegramBotClient, RecordingTelegramClientProxy>();
        var unlinkedProxy = (RecordingTelegramClientProxy)(object)unlinkedClient;

        var updateUnlinked = new Update
        {
            Id = 1001,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = 88001122 },
                From = new TgUser { Id = 88001122, FirstName = "Новачок" },
                Text = "/start"
            }
        };

        await botService.ProcessUpdateWithClientAsync(unlinkedClient, updateUnlinked, CancellationToken.None);

        Assert.NotEmpty(unlinkedProxy.SentMessages);
        var sentTexts = string.Join("\n", unlinkedProxy.SentMessages.Select(m => m.Text ?? string.Empty));
        Assert.Contains("Loop Market", sentTexts);
        Assert.Contains(
            unlinkedProxy.SentMessages,
            m => m.ReplyMarkup is ReplyKeyboardMarkup rkm &&
                 rkm.Keyboard.SelectMany(row => row).Any(btn => btn.RequestContact == true));

        // 2. Linked user opens bot directly via /start start
        long linkedChatId = 88003344;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var uniqueEmail = $"tglinked_{Guid.NewGuid():N}@loopmarket.com";
            db.Users.Add(new TradeStorm.Api.Models.User
            {
                FirstName = "Олена",
                LastName = "Коваль",
                Email = uniqueEmail,
                PasswordHash = "hash",
                PhoneNumber = $"38067{Random.Shared.Next(1000000, 9999999)}",
                TelegramChatId = linkedChatId,
                IsPhoneVerified = true,
                IsEmailVerified = true
            });
            await db.SaveChangesAsync();
        }

        var linkedClient = DispatchProxy.Create<ITelegramBotClient, RecordingTelegramClientProxy>();
        var linkedProxy = (RecordingTelegramClientProxy)(object)linkedClient;

        var updateLinked = new Update
        {
            Id = 1002,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = linkedChatId },
                From = new TgUser { Id = linkedChatId, FirstName = "Олена" },
                Text = "/start start"
            }
        };

        await botService.ProcessUpdateWithClientAsync(linkedClient, updateLinked, CancellationToken.None);

        Assert.NotEmpty(linkedProxy.SentMessages);
        var linkedText = string.Join("\n", linkedProxy.SentMessages.Select(m => m.Text ?? string.Empty));
        Assert.Contains("прив'язано", linkedText);
    }

    [Fact]
    public async Task TelegramWebhook_ReturnsNotFoundWhenUnconfigured_AndRejectsInvalidSecretWithUnauthorized()
    {
        // 1. Live HTTP endpoint rejects unauthenticated calls (404 if bot unconfigured, 401 if secret missing/mismatch)
        var liveBotService = _factory.Services.GetRequiredService<TelegramBotService>();
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/telegram/webhook", new { update_id = 1 });
        var expectedLiveStatus = (liveBotService.HasBotToken && !string.IsNullOrEmpty(liveBotService.WebhookSecretToken))
            ? HttpStatusCode.Unauthorized
            : HttpStatusCode.NotFound;
        Assert.Equal(expectedLiveStatus, res.StatusCode);

        // 2. Configured bot token + webhook secret -> missing or wrong secret returns 401 Unauthorized; valid secret returns 200 OK
        IConfiguration testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = "123456789:TEST_BOT_TOKEN_VALUE",
                ["TELEGRAM_BOT_TOKEN"] = "123456789:TEST_BOT_TOKEN_VALUE",
                ["Telegram:WebhookSecret"] = "test-webhook-secret-xyz",
                ["Telegram:WebhookUrl"] = "https://example.invalid"
            })
            .Build();

        using var scope = _factory.Services.CreateScope();
        var configuredBotService = ActivatorUtilities.CreateInstance<TelegramBotService>(scope.ServiceProvider, testConfig);
        Assert.True(configuredBotService.HasBotToken);
        Assert.False(string.IsNullOrEmpty(configuredBotService.WebhookSecretToken));

        var controller = new TelegramWebhookController(configuredBotService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Missing secret header
        controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"update_id\":1}"));
        var missingRes = await controller.ReceiveWebhook(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(missingRes);

        // Wrong secret header
        controller.HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = "wrong-secret";
        controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"update_id\":1}"));
        var wrongRes = await controller.ReceiveWebhook(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(wrongRes);

        // Valid secret header with empty update payload
        controller.HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = configuredBotService.WebhookSecretToken;
        controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"update_id\":2}"));
        var okRes = await controller.ReceiveWebhook(CancellationToken.None);
        Assert.IsType<OkResult>(okRes);
    }

    [Fact]
    public async Task Login_UnverifiedUser_ReturnsVerificationTicket_AcceptedByVerifyStatus()
    {
        var client = _factory.CreateClient();
        var email = $"unver_{Guid.NewGuid():N}@example.org";
        var phone = $"38093{Random.Shared.Next(1000000, 9999999)}";
        var password = "VerifyTicketPass123!";

        var regRes = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Марія",
            LastName = "Савченко",
            Email = email,
            Password = password,
            PhoneNumber = phone
        });
        regRes.EnsureSuccessStatusCode();

        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsPhoneVerified = false;
            user.IsEmailVerified = false;
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        using var loginDoc = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        Assert.False(loginDoc.RootElement.TryGetProperty("token", out _));
        Assert.False(loginDoc.RootElement.GetProperty("isPhoneVerified").GetBoolean());
        Assert.False(loginDoc.RootElement.GetProperty("isEmailVerified").GetBoolean());
        var ticket = loginDoc.RootElement.GetProperty("verificationTicket").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ticket));

        var statusRes = await client.GetAsync($"/api/users/verify-status?userId={userId}&ticket={Uri.EscapeDataString(ticket!)}");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        using var statusDoc = JsonDocument.Parse(await statusRes.Content.ReadAsStringAsync());
        Assert.False(statusDoc.RootElement.GetProperty("isVerified").GetBoolean());

        var badTicketRes = await client.GetAsync($"/api/users/verify-status?userId={userId}&ticket=invalid_ticket_value");
        Assert.Equal(HttpStatusCode.Unauthorized, badTicketRes.StatusCode);
    }

    [Fact]
    public async Task AnnouncementCache_InvalidatesImmediately_WhenUpdatedOrDeleted()
    {
        var (adminClient, _, _) = await CreateVerifiedUserAsync(UserRoles.Admin);
        var pubClient = _factory.CreateClient();

        // 1. Create announcement A and warm the cache on home
        var createA = await adminClient.PostAsJsonAsync("/api/admin/announcement", new SiteAnnouncementDto
        {
            Title = "Акція A",
            Message = "Перший кешований банер",
            Type = "info",
            TargetPage = "home",
            Placement = "top",
            Duration = "24h",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.OK, createA.StatusCode);
        using var docA = JsonDocument.Parse(await createA.Content.ReadAsStringAsync());
        int idA = docA.RootElement.GetProperty("id").GetInt32();

        var firstFetch = await pubClient.GetAsync("/api/admin/announcement/active?page=home");
        Assert.Equal(HttpStatusCode.OK, firstFetch.StatusCode);
        using var firstDoc = JsonDocument.Parse(await firstFetch.Content.ReadAsStringAsync());
        Assert.True(firstDoc.RootElement.GetProperty("hasActive").GetBoolean());
        Assert.Equal("Акція A", firstDoc.RootElement.GetProperty("infoBanner").GetProperty("title").GetString());

        // 2. Replace with announcement B and verify cache invalidates immediately
        var createB = await adminClient.PostAsJsonAsync("/api/admin/announcement", new SiteAnnouncementDto
        {
            Title = "Акція B",
            Message = "Оновлений банер",
            Type = "promo",
            TargetPage = "home",
            Placement = "top",
            Duration = "24h",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.OK, createB.StatusCode);
        using var docB = JsonDocument.Parse(await createB.Content.ReadAsStringAsync());
        int idB = docB.RootElement.GetProperty("id").GetInt32();

        var secondFetch = await pubClient.GetAsync("/api/admin/announcement/active?page=home");
        Assert.Equal(HttpStatusCode.OK, secondFetch.StatusCode);
        using var secondDoc = JsonDocument.Parse(await secondFetch.Content.ReadAsStringAsync());
        Assert.True(secondDoc.RootElement.GetProperty("hasActive").GetBoolean());
        Assert.Equal("Акція B", secondDoc.RootElement.GetProperty("infoBanner").GetProperty("title").GetString());

        // 3. Delete B (and A just in case) and confirm cache reflects removal immediately
        await adminClient.DeleteAsync($"/api/admin/announcement/{idA}");
        var delB = await adminClient.DeleteAsync($"/api/admin/announcement/{idB}");
        Assert.Equal(HttpStatusCode.OK, delB.StatusCode);

        var afterDelFetch = await pubClient.GetAsync("/api/admin/announcement/active?page=home");
        Assert.Equal(HttpStatusCode.OK, afterDelFetch.StatusCode);
        using var afterDelDoc = JsonDocument.Parse(await afterDelFetch.Content.ReadAsStringAsync());
        Assert.False(afterDelDoc.RootElement.GetProperty("hasActive").GetBoolean());
    }
}
