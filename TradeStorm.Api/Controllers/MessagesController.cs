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
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MessagesController(AppDbContext context)
    {
        _context = context;
    }

    // Отримати історію повідомлень між поточним користувачем та співрозмовником
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

                bool isWaiting = lastMessage != null && lastMessage.SenderId == partnerId;

                dialogsList.Add(new DialogDto
                {
                    Id = partner.Id,
                    FirstName = partner.FirstName,
                    LastName = partner.LastName,
                    AvatarUrl = partner.AvatarUrl,
                    PhoneNumber = partner.PhoneNumber,
                    LastMessage = lastMessage != null ? lastMessage.Text : "Немає повідомлень",
                    LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue,
                    IsWaiting = isWaiting
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
}
