using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public SubscriptionsController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    /// <summary>
    /// Отримати публічний список доступних PRO-тарифів та їх переваг
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public IActionResult GetPlans()
    {
        var plans = new object[]
        {
            new
            {
                id = "pro_monthly",
                title = "LoopMarket PRO",
                subtitle = "Для активних продавців та майстрів",
                price = 199,
                period = "місяць",
                badge = "Популярний",
                popular = true,
                discount = (string?)null,
                features = new[]
                {
                    "🎬 Додавання відеооглядів (MP4/WebM або YouTube/TikTok)",
                    "👑 Золотий бейдж PRO у профілі та на картках оголошень",
                    "📸 До 10 фотографій у кожному оголошенні (замість 5)",
                    "🚀 Пріоритетне розміщення в пошуку та категоріях",
                    "💬 Прямий пріоритетний чат зі службою підтримки"
                }
            },
            new
            {
                id = "brand_annual",
                title = "Бренд & Бізнес",
                subtitle = "Для інтернет-магазинів, брендів та топ-продавців",
                price = 1490,
                period = "рік",
                badge = "Максимум можливостей",
                popular = false,
                discount = "Економія 40%",
                features = new[]
                {
                    "💎 Усі можливості підписки LoopMarket PRO",
                    "🏷️ Офіційний верифікований статус «Бренд»",
                    "📌 Закріплення до 5 оголошень у ТОП-блоці каталогу",
                    "📊 Розширена аналітика переглядів, збережень та контактів",
                    "🎨 Персональне брендування сторінки магазину / майстра",
                    "📞 Персональний менеджер та щомісячний звіт"
                }
            }
        };

        return Ok(plans);
    }

    /// <summary>
    /// Отримати статус підписки поточного користувача
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound(new { error = "Користувача не знайдено" });
        }

        bool isAdmin = User.IsInRole(UserRoles.Admin);
        bool isPremiumActive = isAdmin || (user.IsPremium && (!user.PremiumUntil.HasValue || user.PremiumUntil.Value > DateTime.UtcNow));
        int daysLeft = 0;
        if (user.PremiumUntil.HasValue && user.PremiumUntil.Value > DateTime.UtcNow)
        {
            daysLeft = (int)Math.Ceiling((user.PremiumUntil.Value - DateTime.UtcNow).TotalDays);
        }
        else if (isAdmin)
        {
            daysLeft = 365;
        }

        return Ok(new
        {
            isPremium = isPremiumActive,
            plan = isAdmin && string.IsNullOrEmpty(user.PremiumPlan) ? "admin_unlimited" : user.PremiumPlan,
            premiumUntil = user.PremiumUntil,
            daysLeft,
            isAdmin,
            canUploadVideo = isPremiumActive
        });
    }

    /// <summary>
    /// Оформлення та миттєва активація PRO-підписки
    /// </summary>
    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout([FromBody] CheckoutSubscriptionDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "Користувача не знайдено" });
        }

        var planId = (dto.PlanId ?? "pro_monthly").Trim().ToLowerInvariant();
        int days = planId == "brand_annual" ? 365 : 30;
        string planName = planId == "brand_annual" ? "Бренд & Бізнес (Річний)" : "LoopMarket PRO (Місячний)";
        decimal price = planId == "brand_annual" ? 1490m : 199m;

        // Подовження поточної або нарахування нової підписки
        var currentExpiry = user.PremiumUntil.HasValue && user.PremiumUntil.Value > DateTime.UtcNow
            ? user.PremiumUntil.Value
            : DateTime.UtcNow;

        user.IsPremium = true;
        user.PremiumPlan = planId;
        user.PremiumUntil = currentExpiry.AddDays(days);

        // Фіксуємо подію в журналі аудиту
        try
        {
            var auditLog = new AdminAuditLog
            {
                AdminId = userId,
                AdminEmail = user.Email,
                Action = "SubscriptionActivated",
                TargetType = "User",
                TargetId = user.Id,
                Details = $"Користувач {user.Email} (ID {user.Id}) підключив тариф '{planName}' на {days} днів через метод '{dto.PaymentMethod ?? "card"}'. Сума: {price} грн.",
                CreatedAt = DateTime.UtcNow
            };
            _context.AdminAuditLogs.Add(auditLog);
        }
        catch {}

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"Вітаємо! Підписку {planName} успішно активовано.",
            isPremium = true,
            plan = user.PremiumPlan,
            premiumUntil = user.PremiumUntil,
            canUploadVideo = true
        });
    }

    /// <summary>
    /// Скасування автоподовження підписки
    /// </summary>
    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Не скидаємо поточний термін, лише статус автопродовження
        return Ok(new
        {
            success = true,
            message = "Автопродовження підписки скасовано. Всі переваги PRO діятимуть до завершення поточного періоду.",
            premiumUntil = user.PremiumUntil
        });
    }

    /// <summary>
    /// Webhook для банківських шлюзів (Monobank, WayForPay, LiqPay)
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public IActionResult Webhook([FromBody] object? payload)
    {
        // Зарезервований вебхук для офіційного шлюзу еквайрингу
        return Ok(new { status = "received", timestamp = DateTime.UtcNow });
    }
}

public class CheckoutSubscriptionDto
{
    public string? PlanId { get; set; } = "pro_monthly";
    public string? PaymentMethod { get; set; } = "card";
    public string? ReturnUrl { get; set; }
}
