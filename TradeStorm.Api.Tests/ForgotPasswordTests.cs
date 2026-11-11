using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;
using Xunit;
using AppUser = TradeStorm.Api.Models.User;
using TgChat = Telegram.Bot.Types.Chat;
using TgMessage = Telegram.Bot.Types.Message;
using TgUser = Telegram.Bot.Types.User;

namespace TradeStorm.Api.Tests;

public class RecordingEmailService : IEmailService
{
    public record ResetEmailRecord(string ToEmail, string UserName, string Code, string RequestIp);
    public record AlertEmailRecord(string ToEmail, string UserName, string RequestIp);

    public ConcurrentQueue<ResetEmailRecord> ResetCodes { get; } = new();
    public ConcurrentQueue<AlertEmailRecord> ChangedAlerts { get; } = new();

    public Task<bool> SendVerificationCodeAsync(string toEmail, string userName, string code)
        => Task.FromResult(true);

    public Task<bool> SendPasswordResetCodeAsync(string toEmail, string userName, string code, string requestIp)
    {
        ResetCodes.Enqueue(new ResetEmailRecord(toEmail.ToLowerInvariant(), userName, code, requestIp));
        return Task.FromResult(true);
    }

    public Task<bool> SendPasswordChangedAlertAsync(string toEmail, string userName, string requestIp)
    {
        ChangedAlerts.Enqueue(new AlertEmailRecord(toEmail.ToLowerInvariant(), userName, requestIp));
        return Task.FromResult(true);
    }
}

public class ForgotPasswordWebApplicationFactory : CustomWebApplicationFactory
{
    public RecordingEmailService EmailRecorder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailRecorder);
        });
    }
}

public class ForgotPasswordTests : IClassFixture<ForgotPasswordWebApplicationFactory>
{
    private readonly ForgotPasswordWebApplicationFactory _factory;

    public ForgotPasswordTests(ForgotPasswordWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public sealed class CapturedBotMessage
    {
        public string? Text { get; set; }
        public object? ReplyMarkup { get; set; }
    }

    public class BotGuardRecorderProxy : DispatchProxy
    {
        public List<CapturedBotMessage> Messages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;

            if (args != null && args.Length > 0 && args[0] != null)
            {
                var req = args[0]!;
                var textProp = req.GetType().GetProperty("Text");
                if (textProp != null)
                {
                    Messages.Add(new CapturedBotMessage
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

    private async Task<(string Token, string Answer)> SolveCaptchaAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/auth/captcha");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("token").GetString()!;
        var question = doc.RootElement.GetProperty("question").GetString()!;
        var nums = Regex.Matches(question, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
        Assert.Equal(2, nums.Length);
        return (token, (nums[0] + nums[1]).ToString());
    }

    private async Task<AppUser> SeedUserAsync(string password, string phone = "+380971234829", long telegramChatId = 0, string? refreshToken = null)
    {
        var email = $"fp_{Guid.NewGuid():N}@loopmarket.com";
        var client = _factory.CreateClient();
        var regRes = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = "Олег",
            LastName = "Мельник",
            Email = email,
            Password = password,
            PhoneNumber = phone
        });
        regRes.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        user.IsEmailVerified = true;
        user.IsPhoneVerified = true;
        user.TelegramChatId = telegramChatId;
        if (refreshToken != null)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        }
        await db.SaveChangesAsync();
        return user;
    }

    private string PopLatestResetCodeForEmail(string email)
    {
        var normalized = email.ToLowerInvariant();
        var match = _factory.EmailRecorder.ResetCodes.Reverse().FirstOrDefault(r => r.ToEmail == normalized);
        Assert.NotNull(match);
        Assert.Matches(@"^\d{6}$", match!.Code);
        return match.Code;
    }

    [Fact]
    public async Task StartPasswordReset_EnforcesCaptchaHoneypot_AndAntiEnumeration()
    {
        var client = _factory.CreateClient();
        var (token, answer) = await SolveCaptchaAsync(client);

        // Wrong CAPTCHA answer -> rejected
        var wrongCaptchaRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = "nobody@example.com",
            CaptchaToken = token,
            CaptchaAnswer = "9999"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCaptchaRes.StatusCode);

        // Honeypot filled -> rejected
        var honeypotRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = "nobody@example.com",
            HoneypotTrap = "bot-filled-me",
            CaptchaToken = token,
            CaptchaAnswer = answer
        });
        Assert.Equal(HttpStatusCode.BadRequest, honeypotRes.StatusCode);

        // Unknown email + valid CAPTCHA -> neutral 200 response without leaking account existence
        var unknownEmail = $"missing_{Guid.NewGuid():N}@loopmarket.com";
        var okRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = unknownEmail,
            CaptchaToken = token,
            CaptchaAnswer = answer
        });
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);

        using var doc = JsonDocument.Parse(await okRes.Content.ReadAsStringAsync());
        var sessionToken = doc.RootElement.GetProperty("sessionToken").GetString()!;
        Assert.Equal(32, sessionToken.Length);
        Assert.True(doc.RootElement.GetProperty("requiresPhoneCheck").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("requiresTelegramApproval").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.PasswordResetSessions.AnyAsync(s => s.SessionToken == sessionToken));
        Assert.DoesNotContain(_factory.EmailRecorder.ResetCodes, r => r.ToEmail == unknownEmail);
    }

    [Fact]
    public async Task ForgotPassword_EmailAndPhoneOwnership_ResetsPassword_AndRevokesOldTokens()
    {
        const string oldPassword = "OldPassword123!";
        const string newPassword = "NewStrongPassword2026!";
        var user = await SeedUserAsync(oldPassword, phone: "+380971234829", refreshToken: "old-refresh-token-value");
        var client = _factory.CreateClient();

        var (captchaToken, captchaAnswer) = await SolveCaptchaAsync(client);
        var startRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = user.Email,
            CaptchaToken = captchaToken,
            CaptchaAnswer = captchaAnswer
        });
        startRes.EnsureSuccessStatusCode();

        using var startDoc = JsonDocument.Parse(await startRes.Content.ReadAsStringAsync());
        var sessionToken = startDoc.RootElement.GetProperty("sessionToken").GetString()!;
        Assert.True(startDoc.RootElement.GetProperty("requiresPhoneCheck").GetBoolean());

        var code = PopLatestResetCodeForEmail(user.Email);

        // Verify OTP hash in DB is NOT stored in plaintext
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbSession = await db.PasswordResetSessions.FirstAsync(s => s.SessionToken == sessionToken);
            Assert.NotEqual(code, dbSession.OtpHash);
        }

        // Wrong last 4 phone digits even with valid email OTP -> rejected (anti-hijack check)
        var badPhoneRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = code,
            PhoneLastDigits = "0000"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badPhoneRes.StatusCode);

        // Correct email OTP + last 4 digits of phone -> issues single-use proof token
        var verifyRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = code,
            PhoneLastDigits = "4829"
        });
        verifyRes.EnsureSuccessStatusCode();

        using var verifyDoc = JsonDocument.Parse(await verifyRes.Content.ReadAsStringAsync());
        Assert.True(verifyDoc.RootElement.GetProperty("verified").GetBoolean());
        var proofToken = verifyDoc.RootElement.GetProperty("resetProofToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(proofToken));

        // Weak password -> rejected
        var weakRes = await client.PostAsJsonAsync("/api/users/forgot-password/confirm", new ConfirmPasswordResetDto
        {
            SessionToken = sessionToken,
            ResetProofToken = proofToken,
            NewPassword = "12345678",
            ConfirmPassword = "12345678"
        });
        Assert.Equal(HttpStatusCode.BadRequest, weakRes.StatusCode);

        // Reusing old password -> rejected
        var sameRes = await client.PostAsJsonAsync("/api/users/forgot-password/confirm", new ConfirmPasswordResetDto
        {
            SessionToken = sessionToken,
            ResetProofToken = proofToken,
            NewPassword = oldPassword,
            ConfirmPassword = oldPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, sameRes.StatusCode);

        // Valid strong new password -> succeeds
        var confirmRes = await client.PostAsJsonAsync("/api/users/forgot-password/confirm", new ConfirmPasswordResetDto
        {
            SessionToken = sessionToken,
            ResetProofToken = proofToken,
            NewPassword = newPassword,
            ConfirmPassword = newPassword
        });
        confirmRes.EnsureSuccessStatusCode();

        // Proof token is single-use: replaying it must fail
        var replayRes = await client.PostAsJsonAsync("/api/users/forgot-password/confirm", new ConfirmPasswordResetDto
        {
            SessionToken = sessionToken,
            ResetProofToken = proofToken,
            NewPassword = "AnotherStrongPassword99!",
            ConfirmPassword = "AnotherStrongPassword99!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replayRes.StatusCode);

        // RefreshToken wiped, session marked completed, audit & notification saved
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.Null(updatedUser!.RefreshToken);

            var dbSession = await db.PasswordResetSessions.FirstAsync(s => s.SessionToken == sessionToken);
            Assert.True(dbSession.IsCompleted);
            Assert.NotNull(dbSession.CompletedAt);

            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == user.Id && a.Action == "PasswordResetCompleted"));
            Assert.True(await db.Notifications.AnyAsync(n => n.UserId == user.Id && n.Title == "Пароль акаунта змінено"));
        }

        // Security alert email recorded asynchronously
        for (int i = 0; i < 20; i++)
        {
            if (_factory.EmailRecorder.ChangedAlerts.Any(a => a.ToEmail == user.Email)) break;
            await Task.Delay(50);
        }
        Assert.Contains(_factory.EmailRecorder.ChangedAlerts, a => a.ToEmail == user.Email);

        // Old password rejected on login; new password succeeds
        var oldLoginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto { Email = user.Email, Password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginRes.StatusCode);

        var newLoginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto { Email = user.Email, Password = newPassword });
        newLoginRes.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task VerifyPasswordReset_FiveFailedAttempts_LocksSessionAndWritesAuditLog()
    {
        var user = await SeedUserAsync("StartPassword123!", phone: "+380971239999");
        var client = _factory.CreateClient();

        var (captchaToken, captchaAnswer) = await SolveCaptchaAsync(client);
        var startRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = user.Email,
            CaptchaToken = captchaToken,
            CaptchaAnswer = captchaAnswer
        });
        startRes.EnsureSuccessStatusCode();

        using var startDoc = JsonDocument.Parse(await startRes.Content.ReadAsStringAsync());
        var sessionToken = startDoc.RootElement.GetProperty("sessionToken").GetString()!;
        var realCode = PopLatestResetCodeForEmail(user.Email);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            var res = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
            {
                SessionToken = sessionToken,
                Code = "000000",
                PhoneLastDigits = "9999"
            });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.Equal(5 - attempt, doc.RootElement.GetProperty("attemptsRemaining").GetInt32());
            Assert.Equal(attempt == 5, doc.RootElement.GetProperty("isRevoked").GetBoolean());
        }

        // 6th attempt even with real OTP & phone -> blocked 403
        var lockedRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = realCode,
            PhoneLastDigits = "9999"
        });
        Assert.Equal(HttpStatusCode.Forbidden, lockedRes.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbSession = await db.PasswordResetSessions.FirstAsync(s => s.SessionToken == sessionToken);
        Assert.True(dbSession.IsRevoked);
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == user.Id && a.Action == "PasswordResetLockedAttempts"));
    }

    [Fact]
    public async Task TelegramGuard_RequiresOwnerApproval_BlocksImpostor_AndAllowsResetWhenApproved()
    {
        const long ownerChatId = 777555111;
        const long impostorChatId = 999888777;
        var user = await SeedUserAsync("GuardPassword123!", phone: "+380971237711", telegramChatId: ownerChatId);
        var client = _factory.CreateClient();

        var (captchaToken, captchaAnswer) = await SolveCaptchaAsync(client);
        var startRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = user.Email,
            CaptchaToken = captchaToken,
            CaptchaAnswer = captchaAnswer
        });
        startRes.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startRes.Content.ReadAsStringAsync());
        var sessionToken = startDoc.RootElement.GetProperty("sessionToken").GetString()!;
        var emailCode = PopLatestResetCodeForEmail(user.Email);

        // Enable Telegram guard on session cleanly (testing guard flow independent of external Telegram bot token env)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbSession = await db.PasswordResetSessions.FirstAsync(s => s.SessionToken == sessionToken);
            dbSession.RequiresTelegramApproval = true;
            await db.SaveChangesAsync();
        }

        // Verify Telegram guard message format with inline approve/deny buttons
        var botService = _factory.Services.GetRequiredService<TelegramBotService>();
        var fakeBotClient = DispatchProxy.Create<ITelegramBotClient, BotGuardRecorderProxy>();
        var recorder = (BotGuardRecorderProxy)(object)fakeBotClient;
        await botService.SendPasswordResetGuardWithClientAsync(fakeBotClient, ownerChatId, sessionToken, user.FirstName, "fp***@loopmarket.com", "127.0.0.1");

        Assert.Single(recorder.Messages);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(recorder.Messages[0].ReplyMarkup);
        var callbacks = keyboard.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains($"pr_ok_{sessionToken}", callbacks);
        Assert.Contains($"pr_no_{sessionToken}", callbacks);

        // Submitting valid Email OTP + Phone before Telegram approval -> 202 Accepted (telegramPending)
        var pendingRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = emailCode,
            PhoneLastDigits = "7711"
        });
        Assert.Equal(HttpStatusCode.Accepted, pendingRes.StatusCode);
        using (var pDoc = JsonDocument.Parse(await pendingRes.Content.ReadAsStringAsync()))
        {
            Assert.True(pDoc.RootElement.GetProperty("telegramPending").GetBoolean());
        }

        // Impostor trying to click pr_ok_ MUST be rejected
        await botService.ProcessUpdateWithClientAsync(fakeBotClient, new Update
        {
            Id = 9001,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = impostorChatId, FirstName = "Attacker" },
                Data = $"pr_ok_{sessionToken}"
            }
        }, CancellationToken.None);

        var statusAfterImpostor = await client.GetAsync($"/api/users/forgot-password/status?sessionToken={sessionToken}");
        statusAfterImpostor.EnsureSuccessStatusCode();
        using (var sDoc = JsonDocument.Parse(await statusAfterImpostor.Content.ReadAsStringAsync()))
        {
            Assert.False(sDoc.RootElement.GetProperty("isTelegramApproved").GetBoolean());
        }

        // Owner clicks pr_ok_ -> unlocks reset proof token
        await botService.ProcessUpdateWithClientAsync(fakeBotClient, new Update
        {
            Id = 9002,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = ownerChatId, FirstName = "Owner" },
                Data = $"pr_ok_{sessionToken}"
            }
        }, CancellationToken.None);

        var statusAfterOwner = await client.GetAsync($"/api/users/forgot-password/status?sessionToken={sessionToken}");
        statusAfterOwner.EnsureSuccessStatusCode();
        using (var sDoc = JsonDocument.Parse(await statusAfterOwner.Content.ReadAsStringAsync()))
        {
            Assert.True(sDoc.RootElement.GetProperty("isTelegramApproved").GetBoolean());
        }

        var finalVerifyRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = emailCode,
            PhoneLastDigits = "7711"
        });
        finalVerifyRes.EnsureSuccessStatusCode();
        using var vDoc = JsonDocument.Parse(await finalVerifyRes.Content.ReadAsStringAsync());
        var proofToken = vDoc.RootElement.GetProperty("resetProofToken").GetString()!;

        var confirmRes = await client.PostAsJsonAsync("/api/users/forgot-password/confirm", new ConfirmPasswordResetDto
        {
            SessionToken = sessionToken,
            ResetProofToken = proofToken,
            NewPassword = "TelegramVerifiedNewPass9!",
            ConfirmPassword = "TelegramVerifiedNewPass9!"
        });
        confirmRes.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TelegramGuard_OwnerPanicDeny_RevokesAllSessionsAndRefreshToken()
    {
        const long ownerChatId = 777555222;
        const long strangerChatId = 999888666;
        var user = await SeedUserAsync("PanicPassword123!", phone: "+380971238822", telegramChatId: ownerChatId, refreshToken: "valid-refresh-token");
        var client = _factory.CreateClient();

        var (captchaToken, captchaAnswer) = await SolveCaptchaAsync(client);
        var startRes = await client.PostAsJsonAsync("/api/users/forgot-password/start", new StartPasswordResetDto
        {
            Email = user.Email,
            CaptchaToken = captchaToken,
            CaptchaAnswer = captchaAnswer
        });
        startRes.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startRes.Content.ReadAsStringAsync());
        var sessionToken = startDoc.RootElement.GetProperty("sessionToken").GetString()!;
        var emailCode = PopLatestResetCodeForEmail(user.Email);

        var botService = _factory.Services.GetRequiredService<TelegramBotService>();
        var fakeBotClient = DispatchProxy.Create<ITelegramBotClient, BotGuardRecorderProxy>();

        // Stranger clicking pr_no_ cannot revoke owner's session or tokens
        await botService.ProcessUpdateWithClientAsync(fakeBotClient, new Update
        {
            Id = 9101,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = strangerChatId, FirstName = "Stranger" },
                Data = $"pr_no_{sessionToken}"
            }
        }, CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbSession = await db.PasswordResetSessions.FirstAsync(s => s.SessionToken == sessionToken);
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.False(dbSession.IsRevoked);
            Assert.Equal("valid-refresh-token", dbUser!.RefreshToken);
        }

        // Real owner clicking pr_no_ revokes session AND wipes RefreshToken immediately
        await botService.ProcessUpdateWithClientAsync(fakeBotClient, new Update
        {
            Id = 9102,
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                From = new TgUser { Id = ownerChatId, FirstName = "Owner" },
                Data = $"pr_no_{sessionToken}"
            }
        }, CancellationToken.None);

        var statusRes = await client.GetAsync($"/api/users/forgot-password/status?sessionToken={sessionToken}");
        statusRes.EnsureSuccessStatusCode();
        using (var sDoc = JsonDocument.Parse(await statusRes.Content.ReadAsStringAsync()))
        {
            Assert.True(sDoc.RootElement.GetProperty("isRevoked").GetBoolean());
        }

        // Attempting verify after owner block -> 403 Forbidden
        var verifyBlockedRes = await client.PostAsJsonAsync("/api/users/forgot-password/verify", new VerifyPasswordResetDto
        {
            SessionToken = sessionToken,
            Code = emailCode,
            PhoneLastDigits = "8822"
        });
        Assert.Equal(HttpStatusCode.Forbidden, verifyBlockedRes.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.Null(dbUser!.RefreshToken);
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == user.Id && a.Action == "PasswordResetDeniedByOwner"));
        }
    }
}
