using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TradeStorm.Api.Data;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers
{
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
        [Authorize]
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
                    isRead = m.IsRead // 🔥 Додай ось це
                })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpGet("dialogs")]
        public async Task<IActionResult> GetDialogs()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null) return Unauthorized();
                int currentUserId = int.Parse(userIdClaim);

                var partnerIds = await _context.Messages
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToListAsync();

                var dialogsList = new List<object>();

                foreach (var partnerId in partnerIds)
                {
                    var partner = await _context.Users.FindAsync(partnerId);
                    if (partner == null) continue;

                    var lastMessage = await _context.Messages
                        .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                                    (m.SenderId == partnerId && m.ReceiverId == currentUserId))
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();

                    // Проверяем, ждет ли сообщение ответа (последнее сообщение отправлено партнером)
                    bool isWaiting = lastMessage != null && lastMessage.SenderId == partnerId;

                    dialogsList.Add(new
                    {
                        id = partner.Id,
                        firstName = partner.FirstName,
                        lastName = partner.LastName,
                        avatarUrl = partner.AvatarUrl,
                        phoneNumber = partner.PhoneNumber,
                        lastMessage = lastMessage != null ? lastMessage.Text : "Немає повідомлень",
                        lastMessageTime = lastMessage != null ? lastMessage.SentAt : default,
                        isWaiting = isWaiting
                    });
                }

                var sortedDialogs = dialogsList
                    .OrderByDescending(d => ((DateTime?)d.GetType().GetProperty("lastMessageTime")?.GetValue(d)) ?? default)
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
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            // Если в модели есть поле IsRead, можно фильтровать: .Where(m => m.ReceiverId == userId && !m.IsRead)
            // Пока считаем все входящие сообщения, адресованные текущему пользователю
            var unreadCount = await _context.Messages
                .Where(m => m.ReceiverId == userId)
                .CountAsync();

            return Ok(new { count = unreadCount });
        }
        [HttpPost("mark-read/{partnerId}")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead(int partnerId, [FromServices] IHubContext<ChatHub> hubContext)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            // Повідомлення, які партнер надіслав нам, а ми їх читаємо
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

                // 🔥 Сповіщаємо відправника (партнера), що його повідомлення тепер прочитані!
                // Шукаємо підключення партнера в SignalR хабі
                // (Або можна передати подію на клієнт партнера)
            }

            return Ok(new { success = true });
        }

        // ВИДАЛЕННЯ ЧАТУ
        [HttpDelete("dialogs/{partnerId}")]
        public async Task<IActionResult> DeleteChat(int partnerId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int currentUserId = int.Parse(userIdClaim);

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
}