using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminOperationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TelegramBotService _telegramService;
    private readonly IHubContext<ChatHub> _hubContext;

    public AdminOperationsController(
        AppDbContext context,
        TelegramBotService telegramService,
        IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _telegramService = telegramService;
        _hubContext = hubContext;
    }

    private int GetAdminId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out int id)) return id;
        throw new System.UnauthorizedAccessException("Адміністратор не авторизований");
    }

    private string GetAdminEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ?? "admin@loopmarket.com";
    }

    private async Task LogAuditAsync(string action, string? targetType = null, int? targetId = null, string? details = null)
    {
        try
        {
            var log = new AdminAuditLog
            {
                AdminId = GetAdminId(),
                AdminEmail = GetAdminEmail(),
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };
            _context.AdminAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audit log error: {ex.Message}");
        }
    }

    // 1. ПУБЛІЧНИЙ БАНЕР ОГОЛОШЕНЬ ПЛАТФОРМИ
    [HttpGet("announcement/active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveAnnouncement()
    {
        var active = await _context.SiteAnnouncements
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (active == null) return Ok(new { hasActive = false });

        return Ok(new
        {
            hasActive = true,
            id = active.Id,
            message = active.Message,
            type = active.Type,
            createdAt = active.CreatedAt
        });
    }

    // 2. ВСТАНОВИТИ НОВИЙ БАНЕР ОГОЛОШЕННЯ
    [HttpPost("announcement")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> SetAnnouncement([FromBody] SiteAnnouncementDto dto)
    {
        // Деактивуємо всі старі
        var old = await _context.SiteAnnouncements.Where(a => a.IsActive).ToListAsync();
        foreach (var a in old) a.IsActive = false;

        var banner = new SiteAnnouncement
        {
            Message = dto.Message,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "warning" : dto.Type,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        _context.SiteAnnouncements.Add(banner);
        await _context.SaveChangesAsync();

        await LogAuditAsync("SetAnnouncement", "Announcement", banner.Id, dto.Message);

        return Ok(new { message = "Банер оголошення успішно активовано на сайті", id = banner.Id });
    }

    // 3. ЗНЯТИ БАНЕР ОГОЛОШЕННЯ
    [HttpDelete("announcement/{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> RemoveAnnouncement(int id)
    {
        var banner = await _context.SiteAnnouncements.FindAsync(id);
        if (banner != null)
        {
            banner.IsActive = false;
            await _context.SaveChangesAsync();
            await LogAuditAsync("RemoveAnnouncement", "Announcement", id, "Банер знято");
        }
        return Ok(new { message = "Оголошення деактивовано" });
    }

    // 4. МАСОВА СИСТЕМНА РОЗСИЛКА (BROADCAST)
    [HttpPost("broadcast")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { error = "Повідомлення розсилки не може бути порожнім" });
        }

        var users = await _context.Users.ToListAsync();
        int inAppCount = 0;
        int telegramCount = 0;

        foreach (var user in users)
        {
            // Внутрішнє сповіщення
            var notif = new Notification
            {
                UserId = user.Id,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? "Оголошення LoopMarket" : dto.Title,
                Message = dto.Message,
                Link = "/index.html",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notif);
            inAppCount++;

            // Telegram сповіщення
            if (dto.SendTelegram && user.TelegramChatId != 0 && user.ReceiveTelegramNotifications)
            {
                try
                {
                    string tgText = $"📢 <b>{dto.Title}</b>\n\n{dto.Message}\n\n<i>З повагою, команда безпеки Loop Market</i>";
                    await _telegramService.SendNotificationAsync(user.TelegramChatId, tgText);
                    telegramCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broadcast TG Error] User {user.Id}: {ex.Message}");
                }
            }
        }

        await _context.SaveChangesAsync();
        await LogAuditAsync("Broadcast", "System", null, $"Надіслано {inAppCount} користувачам, TG: {telegramCount}");

        return Ok(new
        {
            message = "Масову розсилку успішно завершено!",
            inAppDelivered = inAppCount,
            telegramDelivered = telegramCount
        });
    }

    // 5. АУДИТ ЧАТУ ДЛЯ СКАРГИ (ПЕРЕГЛЯД ПЕРЕПИСКИ ПРИ РОЗГЛЯДІ СКАРГИ)
    [HttpGet("/api/complaints/admin/{complaintId}/chat")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetComplaintChatContext(int complaintId)
    {
        var complaint = await _context.Complaints
            .Include(c => c.Reporter)
            .Include(c => c.TargetUser)
            .FirstOrDefaultAsync(c => c.Id == complaintId);

        if (complaint == null) return NotFound(new { error = "Скаргу не знайдено" });

        int u1 = complaint.ReporterId;
        int u2 = complaint.TargetUserId;

        var messages = await _context.Messages
            .Where(m => (m.SenderId == u1 && m.ReceiverId == u2) || (m.SenderId == u2 && m.ReceiverId == u1))
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                senderName = m.SenderId == u1
                    ? $"{complaint.Reporter!.FirstName} {complaint.Reporter.LastName}".Trim()
                    : $"{complaint.TargetUser!.FirstName} {complaint.TargetUser.LastName}".Trim(),
                text = m.Text,
                sentAt = m.SentAt
            })
            .ToListAsync();

        return Ok(new
        {
            complaintId,
            reporterId = u1,
            reporterName = $"{complaint.Reporter?.FirstName} {complaint.Reporter?.LastName}".Trim(),
            targetUserId = u2,
            targetUserName = $"{complaint.TargetUser?.FirstName} {complaint.TargetUser?.LastName}".Trim(),
            messages
        });
    }

    // 6. ТИМЧАСОВИЙ МУТ (MUTE) КОРИСТУВАЧА В ЧАТІ
    [HttpPatch("/api/users/admin/{id}/mute")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> MuteUser(int id, [FromBody] MuteUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        if (dto.Minutes <= 0)
        {
            user.MutedUntil = null;
            await _context.SaveChangesAsync();
            await LogAuditAsync("UnmuteUser", "User", id, "Мут знято");
            return Ok(new { message = $"Обмеження чату для {user.Email} знято", isMuted = false });
        }

        user.MutedUntil = DateTime.UtcNow.AddMinutes(dto.Minutes);
        await _context.SaveChangesAsync();

        await LogAuditAsync("MuteUser", "User", id, $"Замучено на {dto.Minutes} хв");

        return Ok(new
        {
            message = $"Користувачу {user.Email} обмежено чат на {dto.Minutes} хвилин",
            isMuted = true,
            mutedUntil = user.MutedUntil
        });
    }

    // 7. ЗАКРІПИТИ В ТОП / VIP ОГОЛОШЕННЯ
    [HttpPatch("/api/listings/admin/{id}/feature")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ToggleFeatureListing(int id)
    {
        var listing = await _context.Listings.FindAsync(id);
        if (listing == null) return NotFound(new { error = "Оголошення не знайдено" });

        listing.IsFeatured = !listing.IsFeatured;
        await _context.SaveChangesAsync();

        await LogAuditAsync("ToggleFeatureListing", "Listing", id, $"IsFeatured = {listing.IsFeatured}");

        return Ok(new
        {
            message = listing.IsFeatured ? "Оголошення закріплено як ТОП" : "Статус ТОП знято",
            isFeatured = listing.IsFeatured
        });
    }

    // 8. АРБІТРАЖ: ПРИМУСОВЕ ОНОВЛЕННЯ ЗАМОВЛЕННЯ
    [HttpPatch("/api/orders/admin/{id}/force-update")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ForceUpdateOrder(int id, [FromBody] ForceUpdateOrderDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound(new { error = "Замовлення не знайдено" });

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            order.Status = dto.Status;
        }

        if (dto.TrackingNumber != null)
        {
            order.TrackingNumber = dto.TrackingNumber.Trim();
        }

        if (dto.TotalPrice.HasValue && dto.TotalPrice.Value > 0)
        {
            order.TotalPrice = dto.TotalPrice.Value;
        }

        await _context.SaveChangesAsync();
        await LogAuditAsync("ForceUpdateOrder", "Order", id, $"Status={order.Status}, Price={order.TotalPrice}, TTN={order.TrackingNumber}");

        return Ok(new
        {
            message = "Замовлення успішно оновлено оператором",
            orderId = order.Id,
            status = order.Status,
            totalPrice = order.TotalPrice,
            trackingNumber = order.TrackingNumber
        });
    }

    // 9. КЕРУВАННЯ КАТЕГОРІЯМИ
    [HttpPost("categories")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminCreateCategory([FromBody] CategoryAdminDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Назва обов'язкова" });

        var category = new Category { Name = dto.Name.Trim() };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CreateCategory", "Category", category.Id, category.Name);
        return Ok(category);
    }

    [HttpPut("categories/{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminUpdateCategory(int id, [FromBody] CategoryAdminDto dto)
    {
        var cat = await _context.Categories.FindAsync(id);
        if (cat == null) return NotFound(new { error = "Категорію не знайдено" });

        cat.Name = dto.Name.Trim();
        await _context.SaveChangesAsync();

        await LogAuditAsync("UpdateCategory", "Category", id, cat.Name);
        return Ok(cat);
    }

    [HttpDelete("categories/{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminDeleteCategory(int id)
    {
        var cat = await _context.Categories.FindAsync(id);
        if (cat == null) return NotFound(new { error = "Категорію не знайдено" });

        _context.Categories.Remove(cat);
        await _context.SaveChangesAsync();

        await LogAuditAsync("DeleteCategory", "Category", id, cat.Name);
        return Ok(new { message = "Категорію видалено" });
    }

    // 10. ЖУРНАЛ ДІЙ ОПЕРАТОРІВ (AUDIT LOGS)
    [HttpGet("audit-logs")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _context.AdminAuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Ok(logs);
    }

    // 11. ЕКСПОРТ ДАНИХ У CSV
    [HttpGet("export/users")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ExportUsersCsv()
    {
        var users = await _context.Users.OrderBy(u => u.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("ID;Ім'я;Прізвище;Email;Телефон;Роль;Верифікований;Заблокований");

        foreach (var u in users)
        {
            sb.AppendLine($"{u.Id};{u.FirstName};{u.LastName};{u.Email};{u.PhoneNumber};{u.Role};{u.IsPhoneVerified};{u.IsBanned}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"loopmarket_users_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/orders")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ExportOrdersCsv()
    {
        var orders = await _context.Orders
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .Include(o => o.Listing)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID;Товар;Покупець;Продавець;Сума;Статус;ТТН;Дата");

        foreach (var o in orders)
        {
            string buyer = o.Buyer != null ? $"{o.Buyer.FirstName} {o.Buyer.LastName}" : "Покупець";
            string seller = o.Seller != null ? $"{o.Seller.FirstName} {o.Seller.LastName}" : "Продавець";
            string title = o.Listing?.Title ?? "Товар";
            sb.AppendLine($"{o.Id};{title};{buyer};{seller};{o.TotalPrice};{o.Status};{o.TrackingNumber};{o.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"loopmarket_orders_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
