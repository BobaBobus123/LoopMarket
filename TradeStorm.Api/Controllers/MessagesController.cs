using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[Authorize]
[Route("api/messages")]
[ApiController]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("GlobalApiLimit")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MessagesController(AppDbContext context)
    {
        _context = context;
    }

    // Отримати історію повідомлень між поточним користувачем та співрозмовником
        // Відправка повідомлення через REST API
    [HttpPost("send")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MessagesLimit")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, [FromServices] IHubContext<ChatHub> hubContext)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int senderId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Повідомлення не може бути порожнім" });

        if (dto.Text.Length > 2000)
            return BadRequest(new { error = "Повідомлення занадто довге (максимум 2000 символів)" });

        if (senderId == dto.ReceiverId)
            return BadRequest(new { error = "Неможливо надіслати повідомлення самому собі" });

        var sender = await _context.Users.FindAsync(senderId);
        if (sender != null)
        {
            if (sender.IsBanned)
                return StatusCode(403, new { error = "Ваш акаунт заблоковано адміністратором" });
            if (sender.MutedUntil.HasValue && sender.MutedUntil > DateTime.UtcNow)
                return StatusCode(403, new { error = $"Вам обмежено відправку повідомлень до {sender.MutedUntil:yyyy-MM-dd HH:mm}" });
        }

        var receiver = await _context.Users.FindAsync(dto.ReceiverId);
        if (receiver == null)
            return NotFound(new { error = "Отримувача не знайдено" });

        var msg = new Message
        {
            SenderId = senderId,
            ReceiverId = dto.ReceiverId,
            Text = dto.Text.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.Messages.Add(msg);
        await _context.SaveChangesAsync();

        try
        {
            var conns = ChatHub.GetConnectionsForUser(dto.ReceiverId);
            if (conns.Count > 0)
            {
                await hubContext.Clients.Clients(conns).SendAsync("ReceiveMessage", senderId, msg.Text, msg.SentAt);
            }
            else
            {
                await hubContext.Clients.User(dto.ReceiverId.ToString()).SendAsync("ReceiveMessage", senderId, msg.Text, msg.SentAt);
            }
        }
        catch { }

        return Ok(new
        {
            id = msg.Id,
            senderId = msg.SenderId,
            receiverId = msg.ReceiverId,
            text = msg.Text,
            sentAt = msg.SentAt,
            isRead = msg.IsRead
        });
    }

    // Отримати історію повідомлень з підтримкою прогресивного завантаження

    [HttpGet("history/{partnerId}")]
    public async Task<IActionResult> GetMessageHistory(
        int partnerId,
        [FromQuery] int? limit = null,
        [FromQuery] int? beforeId = null,
        [FromQuery] int? afterId = null)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

        var convQuery = _context.Messages
            .AsNoTracking()
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                        (m.SenderId == partnerId && m.ReceiverId == currentUserId));

        if (afterId.HasValue)
        {
            var incremental = await convQuery
                .Where(m => m.Id > afterId.Value)
                .OrderBy(m => m.Id)
                .Take(100)
                .Select(m => new
                {
                    id = m.Id,
                    senderId = m.SenderId,
                    receiverId = m.ReceiverId,
                    text = m.Text,
                    sentAt = m.SentAt,
                    isRead = m.IsRead
                })
                .ToListAsync();

            return Ok(incremental);
        }

        if (limit.HasValue || beforeId.HasValue)
        {
            int pageSize = Math.Clamp(limit ?? 30, 1, 100);
            if (!beforeId.HasValue)
            {
                int totalCount = await convQuery.CountAsync();
                Response.Headers["X-Total-Count"] = totalCount.ToString();
            }

            var pageQuery = beforeId.HasValue
                ? convQuery.Where(m => m.Id < beforeId.Value)
                : convQuery;

            var descBatch = await pageQuery
                .OrderByDescending(m => m.Id)
                .Take(pageSize + 1)
                .Select(m => new
                {
                    id = m.Id,
                    senderId = m.SenderId,
                    receiverId = m.ReceiverId,
                    text = m.Text,
                    sentAt = m.SentAt,
                    isRead = m.IsRead
                })
                .ToListAsync();

            bool hasMore = descBatch.Count > pageSize;
            if (hasMore) descBatch.RemoveAt(descBatch.Count - 1);
            descBatch.Reverse();

            Response.Headers["X-Has-More"] = hasMore.ToString().ToLowerInvariant();
            return Ok(descBatch);
        }

        var messages = await convQuery
            .OrderBy(m => m.SentAt)
            .ThenBy(m => m.Id)
            .Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                receiverId = m.ReceiverId,
                text = m.Text,
                sentAt = m.SentAt,
                isRead = m.IsRead
            })
            .ToListAsync();

        Response.Headers["X-Total-Count"] = messages.Count.ToString();
        Response.Headers["X-Has-More"] = "false";
        return Ok(messages);
    }

    [HttpGet("dialogs")]
    public async Task<IActionResult> GetDialogs([FromQuery] int offset = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out int currentUserId)) return Unauthorized();

            int safeOffset = Math.Max(0, offset);
            int safeLimit = Math.Clamp(limit, 1, 100);

            var convoQuery = _context.Messages
                .AsNoTracking()
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    PartnerId = g.Key,
                    LastMsgId = g.Max(m => m.Id)
                })
                .OrderByDescending(x => x.LastMsgId);

            int totalCount = await convoQuery.CountAsync();
            var pageEntries = await convoQuery
                .Skip(safeOffset)
                .Take(safeLimit)
                .ToListAsync();

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Has-More"] = (safeOffset + pageEntries.Count < totalCount).ToString().ToLowerInvariant();

            if (!pageEntries.Any())
            {
                return Ok(new List<DialogDto>());
            }

            var pagePartnerIds = pageEntries.Select(x => x.PartnerId).ToList();
            var lastMsgIds = pageEntries.Select(x => x.LastMsgId).ToList();

            var lastMessages = await _context.Messages
                .AsNoTracking()
                .Where(m => lastMsgIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            var unreadMap = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ReceiverId == currentUserId && !m.IsRead && pagePartnerIds.Contains(m.SenderId))
                .GroupBy(m => m.SenderId)
                .Select(g => new { PartnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PartnerId, x => x.Count);

            var partners = await _context.Users
                .AsNoTracking()
                .Where(u => pagePartnerIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.AvatarUrl,
                    u.PhoneNumber
                })
                .ToDictionaryAsync(u => u.Id);

            var dialogsList = new List<DialogDto>();

            foreach (var entry in pageEntries)
            {
                if (!partners.TryGetValue(entry.PartnerId, out var partner)) continue;

                lastMessages.TryGetValue(entry.LastMsgId, out var lastMessage);
                unreadMap.TryGetValue(entry.PartnerId, out int unreadCount);

                string snippet = "Немає повідомлень";
                if (lastMessage != null && !string.IsNullOrEmpty(lastMessage.Text))
                {
                    snippet = lastMessage.Text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                        ? "📷 Фотографія"
                        : lastMessage.Text;
                }

                dialogsList.Add(new DialogDto
                {
                    Id = partner.Id,
                    FirstName = partner.FirstName,
                    LastName = partner.LastName,
                    AvatarUrl = partner.AvatarUrl,
                    PhoneNumber = PhoneNumberHelper.Normalize(partner.PhoneNumber),
                    LastMessage = snippet,
                    LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue,
                    IsWaiting = unreadCount > 0,
                    UnreadCount = unreadCount
                });
            }

            var sortedDialogs = dialogsList
                .OrderByDescending(d => d.LastMessageTime)
                .ToList();

            return Ok(sortedDialogs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ПОМИЛКА В GetDialogs: {ex}");
            return StatusCode(500, new { message = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // Отримати кількість непрочитаних повідомлень
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
        {
            return Unauthorized();
        }

        var unreadCount = await _context.Messages
            .Where(m => m.ReceiverId == userId && !m.IsRead)
            .CountAsync();

        return Ok(new { count = unreadCount });
    }

    [HttpPost("mark-read/{partnerId}")]
    public async Task<IActionResult> MarkAsRead(int partnerId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var unreadMessages = await _context.Messages
            .Where(m => m.SenderId == partnerId && m.ReceiverId == userId && !m.IsRead)
            .ToListAsync();

        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    // ПОЗНАЧИТИ ВСІ ПОВІДОМЛЕННЯ ЯК ПРОЧИТАНІ
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var unreadMessages = await _context.Messages
            .Where(m => m.ReceiverId == userId && !m.IsRead)
            .ToListAsync();

        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, markedCount = unreadMessages.Count });
    }

    // ВИДАЛЕННЯ ЧАТУ
    [HttpDelete("dialogs/{partnerId}")]
    public async Task<IActionResult> DeleteChat(int partnerId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out int currentUserId)) return Unauthorized();

        var messages = await _context.Messages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                        (m.SenderId == partnerId && m.ReceiverId == currentUserId))
            .ToListAsync();

        if (messages.Any())
        {
            _context.Messages.RemoveRange(messages);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }
}

public class SendMessageDto
{
    public int ReceiverId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class DialogDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; }
    public bool IsWaiting { get; set; }
    public int UnreadCount { get; set; }
}
