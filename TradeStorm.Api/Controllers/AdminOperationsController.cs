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

    private bool IsAdminAuthenticated(string? queryToken = null)
    {
        if (User.IsInRole(UserRoles.Admin)) return true;

        var token = queryToken;
        if (string.IsNullOrWhiteSpace(token) && Request.Cookies.TryGetValue("loop_admin_session", out var cookieTok))
        {
            token = cookieTok;
        }

        if (string.IsNullOrWhiteSpace(token) && Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.ToString().Substring(7).Trim();
        }

        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwt = handler.ReadJwtToken(token);
                var roleClaim = jwt.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Role ||
                    c.Type == "role" ||
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

                return string.Equals(roleClaim, UserRoles.Admin, StringComparison.OrdinalIgnoreCase) && jwt.ValidTo > DateTime.UtcNow;
            }
        }
        catch { }

        return false;
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
    // 1. ПУБЛІЧНИЙ БАНЕР ТА СПИСОК АКТИВНИХ ОГОЛОШЕНЬ ПЛАТФОРМИ
    [HttpGet("announcement/active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveAnnouncement([FromQuery] string? page = null, [FromQuery] string? placement = null)
    {
        var now = DateTime.UtcNow;
        var query = _context.SiteAnnouncements
            .Where(a => a.IsActive && (!a.ExpiresAt.HasValue || a.ExpiresAt.Value > now));

        if (!string.IsNullOrWhiteSpace(page) && page != "all")
        {
            query = query.Where(a => a.TargetPage == "all" || a.TargetPage == page);
        }
        if (!string.IsNullOrWhiteSpace(placement) && placement != "all")
        {
            query = query.Where(a => a.Placement == placement);
        }

        var activeList = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        var primary = activeList.FirstOrDefault();

        if (primary == null) return Ok(new { hasActive = false, announcements = new object[0] });

        return Ok(new
        {
            hasActive = true,
            id = primary.Id,
            title = primary.Title,
            message = primary.Message,
            type = primary.Type,
            targetPage = primary.TargetPage,
            placement = primary.Placement,
            actionText = primary.ActionText,
            actionUrl = primary.ActionUrl,
            imageUrl = primary.ImageUrl,
            expiresAt = primary.ExpiresAt,
            createdAt = primary.CreatedAt,
            announcements = activeList.Select(a => new
            {
                id = a.Id,
                title = a.Title,
                message = a.Message,
                type = a.Type,
                targetPage = a.TargetPage,
                placement = a.Placement,
                actionText = a.ActionText,
                actionUrl = a.ActionUrl,
                imageUrl = a.ImageUrl,
                expiresAt = a.ExpiresAt,
                createdAt = a.CreatedAt
            })
        });
    }

    // 1.1 СПИСОК УСІХ ОГОЛОШЕНЬ ДЛЯ АДМІН-ПАНЕЛІ
    [HttpGet("announcements")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAllAnnouncements()
    {
        var now = DateTime.UtcNow;
        var list = await _context.SiteAnnouncements
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Ok(list.Select(a => new
        {
            id = a.Id,
            title = a.Title,
            message = a.Message,
            type = a.Type,
            targetPage = a.TargetPage,
            placement = a.Placement,
            actionText = a.ActionText,
            actionUrl = a.ActionUrl,
            imageUrl = a.ImageUrl,
            expiresAt = a.ExpiresAt,
            isActive = a.IsActive,
            isExpired = a.ExpiresAt.HasValue && a.ExpiresAt.Value <= now,
            createdAt = a.CreatedAt
        }));
    }

    // 2. ВСТАНОВИТИ / СТВОРИТИ НОВЕ ОГОЛОШЕННЯ АБО БАНЕР
    [HttpPost("announcement")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> SetAnnouncement([FromBody] SiteAnnouncementDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            dto.Message = !string.IsNullOrWhiteSpace(dto.Title) 
                ? dto.Title 
                : (!string.IsNullOrWhiteSpace(dto.ImageUrl) ? "Рекламний банер LoopMarket" : "Повідомлення платформи");
        }

        DateTime? expiresAt = null;
        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Duration))
        {
            var dur = dto.Duration.Trim().ToLowerInvariant();
            if (dur == "24h" || dur == "1d") expiresAt = now.AddHours(24);
            else if (dur == "3d") expiresAt = now.AddDays(3);
            else if (dur == "7d" || dur == "1w") expiresAt = now.AddDays(7);
            else if (dur == "30d" || dur == "1m") expiresAt = now.AddDays(30);
            else if (dur == "forever" || dur == "none" || dur == "unlimited") expiresAt = null;
        }

        var targetPage = string.IsNullOrWhiteSpace(dto.TargetPage) ? "all" : dto.TargetPage.Trim().ToLowerInvariant();
        var placement = string.IsNullOrWhiteSpace(dto.Placement) ? "top" : dto.Placement.Trim().ToLowerInvariant();
        var type = string.IsNullOrWhiteSpace(dto.Type) ? "warning" : dto.Type.Trim().ToLowerInvariant();

        var banner = new SiteAnnouncement
        {
            Title = (dto.Title ?? string.Empty).Trim(),
            Message = dto.Message.Trim(),
            Type = type,
            TargetPage = targetPage,
            Placement = placement,
            ExpiresAt = expiresAt,
            ActionText = string.IsNullOrWhiteSpace(dto.ActionText) ? null : dto.ActionText.Trim(),
            ActionUrl = string.IsNullOrWhiteSpace(dto.ActionUrl) ? null : dto.ActionUrl.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = now
        };

        // Якщо створюється новий рекламний банер — деактивуємо старі рекламні оголошення
        if (type == "promo" || placement.StartsWith("sidebar") || placement == "catalog")
        {
            var oldAds = await _context.SiteAnnouncements
                .Where(a => a.IsActive && (a.Type == "promo" || a.Placement.StartsWith("sidebar") || a.Placement == "catalog"))
                .ToListAsync();
            foreach (var old in oldAds) old.IsActive = false;
        }

        _context.SiteAnnouncements.Add(banner);
        await _context.SaveChangesAsync();

        await LogAuditAsync("SetAnnouncement", "Announcement", banner.Id, $"[{placement} / {targetPage}] {dto.Message}");

        return Ok(new
        {
            message = "Оголошення успішно опубліковано на сайті",
            id = banner.Id,
            expiresAt = banner.ExpiresAt,
            placement = banner.Placement,
            targetPage = banner.TargetPage
        });
    }

    // 3. ЗНЯТИ ОГОЛОШЕННЯ / ДЕАКТИВУВАТИ
    [HttpDelete("announcement/{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> RemoveAnnouncement(int id)
    {
        SiteAnnouncement? banner = null;
        if (id > 0)
        {
            banner = await _context.SiteAnnouncements.FindAsync(id);
        }

        if (banner == null)
        {
            banner = await _context.SiteAnnouncements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (banner != null)
        {
            banner.IsActive = false;
            await _context.SaveChangesAsync();
            await LogAuditAsync("RemoveAnnouncement", "Announcement", banner.Id, "Оголошення знято з сайту");
            return Ok(new { message = $"Оголошення #{banner.Id} деактивовано", id = banner.Id });
        }

        return Ok(new { message = "Активних оголошень не знайдено" });
    }

    // 3.1 ВИДАЛИТИ ОГОЛОШЕННЯ НАЗАВЖДИ
    [HttpDelete("announcement/{id}/permanent")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> DeleteAnnouncementPermanent(int id)
    {
        var banner = await _context.SiteAnnouncements.FindAsync(id);
        if (banner != null)
        {
            _context.SiteAnnouncements.Remove(banner);
            await _context.SaveChangesAsync();
            await LogAuditAsync("DeleteAnnouncement", "Announcement", id, "Оголошення видалено назавжди");
        }
        return Ok(new { message = "Оголошення видалено" });
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

    // 10. ОФІЦІЙНА ВІДПОВІДЬ НА СКАРГУ / ТІКЕТ ПІДТРИМКИ
    [HttpPost("complaints/{id}/reply")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ReplyToComplaint(int id, [FromBody] ReplyComplaintDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { error = "Повідомлення не може бути порожнім" });
        }

        var complaint = await _context.Complaints
            .Include(c => c.Reporter)
            .Include(c => c.TargetUser)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) return NotFound(new { error = "Тікет / скаргу не знайдено" });

        var targetUser = complaint.Reporter ?? complaint.TargetUser;
        if (targetUser == null)
        {
            return BadRequest(new { error = "Одержувача звернення не знайдено в базі даних" });
        }

        int adminId = GetAdminId();
        string adminEmail = GetAdminEmail();

        // 1. Повідомлення у внутрішній чат сайту (messages.html)
        if (dto.SendInternalChat)
        {
            var chatMsg = new Message
            {
                SenderId = adminId,
                ReceiverId = targetUser.Id,
                Text = $"🛡️ [Служба підтримки Loop Market • {complaint.Reason}]\n\n{dto.Message.Trim()}\n\nЗ повагою, команда платформи Loop Market",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Messages.Add(chatMsg);

            try
            {
                await _hubContext.Clients.User(targetUser.Id.ToString()).SendAsync("ReceiveMessage", new
                {
                    id = chatMsg.Id,
                    senderId = adminId,
                    senderName = "Служба підтримки Loop Market",
                    text = chatMsg.Text,
                    sentAt = chatMsg.SentAt
                });
            }
            catch { }
        }

        // 2. Внутрішнє системне сповіщення (Notifications)
        var notif = new Notification
        {
            UserId = targetUser.Id,
            Title = $"💬 Відповідь підтримки на {complaint.Reason}",
            Message = dto.Message.Trim(),
            Link = "/messages.html",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);

        // 3. Офіційне сповіщення через Telegram-бота (без передачі приватних контактів адміна!)
        if (dto.SendTelegram && targetUser.TelegramChatId != 0)
        {
            try
            {
                string tgText = $"💬 <b>ОФІЦІЙНА ВІДПОВІДЬ ПІДТРИМКИ LOOP MARKET</b>\n" +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"🎫 <b>Звернення:</b> <code>{complaint.Reason}</code>\n\n" +
                                $"{System.Net.WebUtility.HtmlEncode(dto.Message.Trim())}\n\n" +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"<i>Ви можете продовжити діалог у внутрішньому чаті на сайті: https://loopmarket-x0uo.onrender.com/messages.html</i>";

                await _telegramService.SendNotificationAsync(targetUser.TelegramChatId, tgText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Support Reply TG Error] {ex.Message}");
            }
        }

        // 4. Оновлюємо статус тікета
        if (dto.MarkResolved)
        {
            complaint.Status = ComplaintStatus.Resolved;
        }
        complaint.ResolutionNotes = $"Офіційна відповідь {adminEmail}: {dto.Message.Trim()}";
        await _context.SaveChangesAsync();

        await LogAuditAsync("ReplyComplaint", "Complaint", id, $"Офіційна відповідь {targetUser.Email}: {dto.Message.Trim()}");

        return Ok(new
        {
            message = "Офіційну відповідь успішно доставлено користувачеві!",
            complaintId = id,
            status = complaint.Status
        });
    }

    // 11. ЖУРНАЛ ДІЙ ОПЕРАТОРІВ (AUDIT LOGS) З ФІЛЬТРАЦІЄЮ ТА ПОШУКОМ
    [HttpGet("audit-logs")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? action, [FromQuery] string? search)
    {
        var query = _context.AdminAuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action) && action != "All")
        {
            query = query.Where(l => l.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.AdminEmail.ToLower().Contains(s) ||
                                     l.Action.ToLower().Contains(s) ||
                                     (l.Details != null && l.Details.ToLower().Contains(s)));
        }

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(150)
            .ToListAsync();

        return Ok(logs);
    }

    // 12. ЕКСПОРТ ДАНИХ У CSV (ПІДТРИМКА БРАУЗЕРНОГО ЗАВАНТАЖЕННЯ З UTF-8 BOM ДЛЯ EXCEL)
    [HttpGet("export/users")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportUsersCsv([FromQuery] string? token)
    {
        if (!IsAdminAuthenticated(token)) return Unauthorized(new { error = "Необхідні права адміністратора" });

        var users = await _context.Users.OrderBy(u => u.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("ID;Ім'я;Прізвище;Email;Телефон;Роль;Верифікований;Заблокований;TelegramChatId");

        foreach (var u in users)
        {
            sb.AppendLine($"{u.Id};{u.FirstName};{u.LastName};{u.Email};{u.PhoneNumber};{u.Role};{u.IsPhoneVerified};{u.IsBanned};{u.TelegramChatId}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"loopmarket_users_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/orders")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportOrdersCsv([FromQuery] string? token)
    {
        if (!IsAdminAuthenticated(token)) return Unauthorized(new { error = "Необхідні права адміністратора" });

        var orders = await _context.Orders
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .Include(o => o.Listing)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID;Код_Замовлення;Товар;Покупець;Телефон_Покупця;Продавець;Телефон_Продавця;Сума_Грн;Оплата;Місто;Відділення;Статус;ТТН;Дата");

        foreach (var o in orders)
        {
            string buyer = o.Buyer != null ? $"{o.Buyer.FirstName} {o.Buyer.LastName}" : "Покупець";
            string buyerPhone = o.Buyer?.PhoneNumber ?? "";
            string seller = o.Seller != null ? $"{o.Seller.FirstName} {o.Seller.LastName}" : "Продавець";
            string sellerPhone = o.Seller?.PhoneNumber ?? "";
            string title = (o.Listing?.Title ?? "Товар").Replace(";", ",");
            string city = (o.DeliveryCity ?? "").Replace(";", ",");
            string branch = (o.DeliveryBranch ?? "").Replace(";", ",");
            sb.AppendLine($"{o.Id};{o.GetDisplayCode()};{title};{buyer};{buyerPhone};{seller};{sellerPhone};{o.TotalPrice};{o.PaymentMethod};{city};{branch};{o.Status};{o.TrackingNumber};{o.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"loopmarket_orders_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/listings")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportListingsCsv([FromQuery] string? token)
    {
        if (!IsAdminAuthenticated(token)) return Unauthorized(new { error = "Необхідні права адміністратора" });

        var listings = await _context.Listings
            .Include(l => l.Seller)
            .Include(l => l.Category)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID;Назва;Категорія;Ціна_Грн;Тип;ТОП;Продавець;Email;Телефон;Місто;Дата");

        foreach (var l in listings)
        {
            string seller = l.Seller != null ? $"{l.Seller.FirstName} {l.Seller.LastName}" : "Продавець";
            string cat = l.Category?.Name ?? "Категорія";
            string type = l.CategoryId == 8 ? "Послуга" : "Товар";
            string title = l.Title.Replace(";", ",");
            string loc = (l.Location ?? "").Replace(";", ",");
            sb.AppendLine($"{l.Id};{title};{cat};{l.Price};{type};{l.IsFeatured};{seller};{l.Seller?.Email};{l.Seller?.PhoneNumber};{loc};{l.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"loopmarket_listings_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/complaints")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportComplaintsCsv([FromQuery] string? token)
    {
        if (!IsAdminAuthenticated(token)) return Unauthorized(new { error = "Необхідні права адміністратора" });

        var complaints = await _context.Complaints
            .Include(c => c.Reporter)
            .Include(c => c.TargetUser)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID;Причина;Статус;Заявник;Телефон_Заявника;Відповідач;Деталі;Рішення;Дата");

        foreach (var c in complaints)
        {
            string rep = c.Reporter != null ? $"{c.Reporter.FirstName} {c.Reporter.LastName}" : "Заявник";
            string repPhone = c.Reporter?.PhoneNumber ?? "";
            string tgt = c.TargetUser != null ? $"{c.TargetUser.FirstName} {c.TargetUser.LastName}" : "Користувач";
            string cleanDetails = (c.Details ?? "").Replace("\n", " ").Replace(";", ",");
            string cleanNotes = (c.ResolutionNotes ?? "").Replace("\n", " ").Replace(";", ",");
            string reason = c.Reason.Replace(";", ",");
            sb.AppendLine($"{c.Id};{reason};{c.Status};{rep};{repPhone};{tgt};{cleanDetails};{cleanNotes};{c.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"loopmarket_tickets_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
