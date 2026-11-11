using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;
using Xunit;
using TgChat = Telegram.Bot.Types.Chat;
using TgMessage = Telegram.Bot.Types.Message;
using TgUser = Telegram.Bot.Types.User;

namespace TradeStorm.Api.Tests;

public class EmailAndBotAdminTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailAndBotAdminTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<(Uri? Uri, string Body, string? ApiKeyHeader)> Requests { get; } = new();

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
            string? apiKey = request.Headers.TryGetValues("api-key", out var vals) ? vals.FirstOrDefault() : null;
            Requests.Add((request.RequestUri, body, apiKey));
            return _responder(request);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler);
        }
        public HttpClient CreateClient(string name) => _client;
    }

    public sealed class BotCapturedPayload
    {
        public string? Text { get; set; }
        public object? ReplyMarkup { get; set; }
    }

    public class BotStubProxy : DispatchProxy
    {
        public List<BotCapturedPayload> Messages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;
            if (args != null && args.Length > 0 && args[0] != null)
            {
                var req = args[0]!;
                var textProp = req.GetType().GetProperty("Text");
                if (textProp != null)
                {
                    Messages.Add(new BotCapturedPayload
                    {
                        Text = textProp.GetValue(req)?.ToString(),
                        ReplyMarkup = req.GetType().GetProperty("ReplyMarkup")?.GetValue(req)
                    });
                }
            }

            var returnType = targetMethod.ReturnType;
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = returnType.GetGenericArguments()[0];
                object? innerValue = innerType == typeof(TgMessage)
                    ? new TgMessage()
                    : (innerType.IsValueType || innerType.GetConstructor(Type.EmptyTypes) != null
                        ? Activator.CreateInstance(innerType)
                        : null);
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(innerType)
                    .Invoke(null, new[] { innerValue });
            }
            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }

    private async Task<(HttpClient Client, int UserId, string Email)> CreateVerifiedUserWithRoleAsync(string role, long telegramChatId = 0)
    {
        var client = _factory.CreateClient();
        var email = $"ebt_{Guid.NewGuid():N}@loopmarket.com";
        var phone = $"38097{Random.Shared.Next(1000000, 9999999)}";

        var regRes = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Тест",
            LastName = role,
            Email = email,
            Password = "StrongPassword123!",
            PhoneNumber = phone
        });
        regRes.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.Role = role;
            user.IsPhoneVerified = true;
            user.IsEmailVerified = true;
            user.TelegramChatId = telegramChatId;
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
    }

    [Fact]
    public async Task EmailService_UsesHttpsWebhookAndFallsBackToBrevo_WithoutLyingOnFailure()
    {
        // 1. Webhook fails -> falls back automatically to Brevo HTTPS 443 API -> returns true
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("script.google.com"))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("relay error") };
            }
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"messageId\":\"ok\"}") };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:WebhookUrl"] = "https://script.google.com/macros/s/fake-id/exec",
                ["Email:WebhookSecret"] = "sec123",
                ["Brevo:ApiKey"] = "xkeysib-test-key",
                ["Smtp:User"] = "loopmarketua@gmail.com"
            })
            .Build();

        var service = new EmailService(config, NullLogger<EmailService>.Instance, new StubHttpClientFactory(handler));
        var sent = await service.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "654321");

        Assert.True(sent);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("script.google.com", handler.Requests[0].Uri!.Host);
        Assert.Equal("api.brevo.com", handler.Requests[1].Uri!.Host);
        Assert.Equal("xkeysib-test-key", handler.Requests[1].ApiKeyHeader);
        Assert.Equal("BrevoHttpsApi", service.GetDiagnostics().LastAttempt?.Provider);

        // 2. When ALL configured providers fail, SendVerificationCodeAsync MUST return false (never pretend success)
        var failingHandler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad key") });
        var failConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brevo:ApiKey"] = "bad-key",
                ["Smtp:User"] = "loopmarketua@gmail.com"
            })
            .Build();

        var failService = new EmailService(failConfig, NullLogger<EmailService>.Instance, new StubHttpClientFactory(failingHandler));
        var failSent = await failService.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "111222");

        Assert.False(failSent);
        var diag = failService.GetDiagnostics();
        Assert.True(diag.HasBrevoApiKey);
        Assert.False(diag.LastAttempt!.Success);
        Assert.Contains("401", diag.LastAttempt.Detail);
    }

    [Fact]
    public async Task TelegramBot_EnforcesAdminAuthorizationOnCallbacks_AndSupportsUserModeAndUnlink()
    {
        var botService = _factory.Services.GetRequiredService<TelegramBotService>();
        long admin1ChatId = 661001;
        long admin2ChatId = 661002;
        long intruderChatId = 669999;

        var (_, admin1Id, _) = await CreateVerifiedUserWithRoleAsync(UserRoles.Admin, admin1ChatId);
        var (_, admin2Id, admin2Email) = await CreateVerifiedUserWithRoleAsync(UserRoles.Admin, admin2ChatId);
        var (_, victimId, _) = await CreateVerifiedUserWithRoleAsync(UserRoles.Buyer, 661003);

        var botClient = DispatchProxy.Create<ITelegramBotClient, BotStubProxy>();
        var stub = (BotStubProxy)(object)botClient;

        // 1. Non-admin forging adm_ban callback MUST be blocked
        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7001,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = intruderChatId, FirstName = "Intruder" },
                Data = $"adm_ban_{victimId}"
            }
        }, CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var victim = await db.Users.FindAsync(victimId);
            Assert.False(victim!.IsBanned);
        }

        // 2. Admin runs /admins -> receives overview with unlink buttons and user-mode toggle
        stub.Messages.Clear();
        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7002,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = admin1ChatId },
                From = new TgUser { Id = admin1ChatId, FirstName = "Admin1" },
                Text = "/admins"
            }
        }, CancellationToken.None);

        Assert.Contains(stub.Messages, m => (m.Text ?? "").Contains("АДМІНІСТРАТОРИ В TELEGRAM-БОТІ") && (m.Text ?? "").Contains(admin2Email));
        var adminOverviewMsg = stub.Messages.Last();
        var ikm = Assert.IsType<InlineKeyboardMarkup>(adminOverviewMsg.ReplyMarkup);
        var callbacks = ikm.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains($"adm_unlink_tg_{admin2Id}", callbacks);
        Assert.Contains("adm_mode_user", callbacks);

        // 3. Admin unlinks Admin2's Telegram via callback -> DB TelegramChatId cleared + audit logged
        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7003,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = admin1ChatId, FirstName = "Admin1" },
                Data = $"adm_unlink_tg_{admin2Id}"
            }
        }, CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedAdmin2 = await db.Users.FindAsync(admin2Id);
            Assert.Equal(0, updatedAdmin2!.TelegramChatId);
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.TargetId == admin2Id && a.Action == "TelegramAdminUnlinked"));
        }

        // 4. Admin toggles user mode (/user) -> receives user menu; /admin switches back
        stub.Messages.Clear();
        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7004,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = admin1ChatId },
                From = new TgUser { Id = admin1ChatId, FirstName = "Admin1" },
                Text = "/user"
            }
        }, CancellationToken.None);
        Assert.True(TelegramBotService.AdminUserModeChats.ContainsKey(admin1ChatId));
        Assert.Contains(stub.Messages, m => (m.Text ?? "").Contains("режим звичайного користувача"));

        stub.Messages.Clear();
        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7005,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = admin1ChatId },
                From = new TgUser { Id = admin1ChatId, FirstName = "Admin1" },
                Text = "/admin"
            }
        }, CancellationToken.None);
        Assert.False(TelegramBotService.AdminUserModeChats.ContainsKey(admin1ChatId));
        Assert.True(admin1Id > 0);
    }

    [Fact]
    public async Task AdminOperations_EmailStatusAndTelegramUnlink_EnforceAdminRole()
    {
        var (buyerClient, _, _) = await CreateVerifiedUserWithRoleAsync(UserRoles.Buyer);
        var (adminClient, _, _) = await CreateVerifiedUserWithRoleAsync(UserRoles.Admin, 771001);
        var (_, secondAdminId, _) = await CreateVerifiedUserWithRoleAsync(UserRoles.Admin, 771002);

        // Non-admin blocked
        var buyerDiagRes = await buyerClient.GetAsync("/api/admin/telegram-admins");
        Assert.Equal(HttpStatusCode.Forbidden, buyerDiagRes.StatusCode);

        // Admin sees email status & telegram admins list
        var emailStatusRes = await adminClient.GetAsync("/api/admin/email-status");
        emailStatusRes.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await emailStatusRes.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.TryGetProperty("diagnostics", out _));
        }

        var tgAdminsRes = await adminClient.GetAsync("/api/admin/telegram-admins");
        tgAdminsRes.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await tgAdminsRes.Content.ReadAsStringAsync()))
        {
            var arr = doc.RootElement.GetProperty("adminUsers");
            Assert.Contains(arr.EnumerateArray(), x => x.GetProperty("id").GetInt32() == secondAdminId);
        }

        // Admin can unlink duplicate Telegram from second Admin
        var unlinkRes = await adminClient.PostAsync($"/api/admin/users/{secondAdminId}/unlink-telegram", null);
        unlinkRes.EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reloaded = await db.Users.FindAsync(secondAdminId);
            Assert.Equal(0, reloaded!.TelegramChatId);
        }
    }

    [Fact]
    public async Task EmailWebhook_RejectsHtmlLoginOrMissingDoPostOrDevUrl_AndBotEmailDiagnosticWorks()
    {
        // 1. /dev Apps Script URL rejected with clear hint
        var devConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:WebhookUrl"] = "https://script.google.com/macros/s/fake-deploy/dev",
                ["Smtp:User"] = "loopmarketua@gmail.com"
            })
            .Build();
        var dummyHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });
        var devSvc = new EmailService(devConfig, NullLogger<EmailService>.Instance, new StubHttpClientFactory(dummyHandler));
        Assert.False(await devSvc.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "123456"));
        Assert.Contains("/dev", devSvc.GetDiagnostics().LastAttempt?.Detail ?? "");

        // 2. HTTP 200 HTML "Script function not found: doPost" rejected instead of faking success
        var missingDoPostHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!doctype html><html><body>Script function not found: doPost</body></html>", Encoding.UTF8, "text/html")
        });
        var execConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:WebhookUrl"] = "https://script.google.com/macros/s/fake-deploy/exec",
                ["Smtp:User"] = "loopmarketua@gmail.com"
            })
            .Build();
        var missingDoPostSvc = new EmailService(execConfig, NullLogger<EmailService>.Instance, new StubHttpClientFactory(missingDoPostHandler));
        Assert.False(await missingDoPostSvc.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "123456"));
        Assert.Contains("doPost", missingDoPostSvc.GetDiagnostics().LastAttempt?.Detail ?? "");

        // 3. HTTP 200 JSON {"ok":false,"error":"Quota exceeded"} rejected accurately
        var jsonErrHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":false,\"error\":\"Quota exceeded\"}", Encoding.UTF8, "application/json")
        });
        var jsonErrSvc = new EmailService(execConfig, NullLogger<EmailService>.Instance, new StubHttpClientFactory(jsonErrHandler));
        Assert.False(await jsonErrSvc.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "123456"));
        Assert.Contains("Quota exceeded", jsonErrSvc.GetDiagnostics().LastAttempt?.Detail ?? "");

        // 4. Valid HTTP 200 JSON {"ok":true} succeeds
        var okSvc = new EmailService(execConfig, NullLogger<EmailService>.Instance, new StubHttpClientFactory(dummyHandler));
        Assert.True(await okSvc.SendVerificationCodeAsync("buyer@gmail.com", "Олег", "123456"));
        Assert.Equal("GmailWebhookHttps", okSvc.GetDiagnostics().LastAttempt?.Provider);

        // 5. Telegram bot /email command responds to Admin & non-admin adm_test_email callback is ignored
        var botService = _factory.Services.GetRequiredService<TelegramBotService>();
        long adminChatId = 661099;
        await CreateVerifiedUserWithRoleAsync(UserRoles.Admin, adminChatId);
        var botClient = DispatchProxy.Create<ITelegramBotClient, BotStubProxy>();
        var stub = (BotStubProxy)(object)botClient;

        await botService.ProcessUpdateWithClientAsync(botClient, new Update
        {
            Id = 7099,
            Message = new TgMessage
            {
                Chat = new TgChat { Id = adminChatId },
                From = new TgUser { Id = adminChatId, FirstName = "AdminDiag" },
                Text = "/email"
            }
        }, CancellationToken.None);

        Assert.Contains(stub.Messages, m => (m.Text ?? "").Contains("ДІАГНОСТИКА ПОШТОВОГО СЕРВІСУ") && (m.Text ?? "").Contains("GMAIL_WEBHOOK_URL"));
    }
}
