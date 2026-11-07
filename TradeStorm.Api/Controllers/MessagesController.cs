using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Models;

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
            await hubContext.Clients.User(dto.ReceiverId.ToString()).SendAsync("ReceiveMessage", senderId, msg.Text, msg.SentAt);
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

    // Отримати історію повідомлень

    [HttpGet("history/{partnerId}")]
    public async Task<IActionResult> GetMessageHistory(int partnerId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

        var messages = await _context.Messages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                        (m.SenderId == partnerId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.SentAt)
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

        return Ok(messages);
    }

    [HttpGet("dialogs")]
    public async Task<IActionResult> GetDialogs()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out int currentUserId)) return Unauthorized();

            var partnerIds = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            if (!partnerIds.Any())
            {
                return Ok(new List<DialogDto>());
            }

            var partners = await _context.Users
                .Where(u => partnerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var dialogsList = new List<DialogDto>();

            foreach (var partnerId in partnerIds)
            {
                if (!partners.TryGetValue(partnerId, out var partner)) continue;

                var lastMessage = await _context.Messages
                    .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                                (m.SenderId == partnerId && m.ReceiverId == currentUserId))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unreadCount = await _context.Messages
                    .Where(m => m.SenderId == partnerId && m.ReceiverId == currentUserId && !m.IsRead)
                    .CountAsync();

                bool isWaiting = unreadCount > 0;

                dialogsList.Add(new DialogDto
                {
                    Id = partner.Id,
                    FirstName = partner.FirstName,
                    LastName = partner.LastName,
                    AvatarUrl = partner.AvatarUrl,
                    PhoneNumber = partner.PhoneNumber,
                    LastMessage = lastMessage != null ? lastMessage.Text : "Немає повідомлень",
                    LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue,
                    IsWaiting = isWaiting,
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
