using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _context;
    private readonly TelegramBotService _telegramService;
    private static readonly ConcurrentDictionary<int, string> _userConnections = new();

    public ChatHub(AppDbContext context, TelegramBotService telegramService)
    {
        _context = context;
        _telegramService = telegramService;
    }

    public override Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            _userConnections[userId] = Context.ConnectionId;
            Console.WriteLine($"✅ Користувач ID {userId} автентифікований в SignalR (ConnectionId: {Context.ConnectionId})");
        }
        return base.OnConnectedAsync();
    }

    public Task RegisterUser(int userId)
    {
        // Якщо користувач автентифікований, перевіряємо відповідність
        var authUserIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(authUserIdStr, out int authUserId) && authUserId != userId)
        {
            Console.WriteLine($"⚠️ Спроба підміни ID: токен={authUserId}, запит={userId}");
            userId = authUserId;
        }

        _userConnections[userId] = Context.ConnectionId;
        Console.WriteLine($"✅ Користувач ID {userId} зареєстрований в SignalR (ConnectionId: {Context.ConnectionId})");
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var pair in _userConnections)
        {
            if (pair.Value == Context.ConnectionId)
            {
                _userConnections.TryRemove(pair.Key, out _);
                break;
            }
        }
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendTyping(string senderIdStr, string receiverIdStr)
    {
        var authUserIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(authUserIdStr))
        {
            senderIdStr = authUserIdStr;
        }

        if (int.TryParse(receiverIdStr, out int receiverId))
        {
            if (_userConnections.TryGetValue(receiverId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("UserIsTyping", senderIdStr);
            }
        }
    }

    public async Task SendMessage(string senderIdStr, string receiverIdStr, string text)
    {
        try
        {
            // Якщо користувач автентифікований, беремо його справжній ID з токена для захисту від підміни
            var authUserIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(authUserIdStr))
            {
                senderIdStr = authUserIdStr;
            }

            if (int.TryParse(senderIdStr, out int senderId) && int.TryParse(receiverIdStr, out int receiverId))
            {
                var sender = await _context.Users.FindAsync(senderId);
                if (sender != null && sender.IsBanned)
                {
                    await Clients.Caller.SendAsync("ErrorMessage", "Ваш акаунт заблоковано.");
                    return;
                }
                if (sender != null && sender.MutedUntil.HasValue && sender.MutedUntil.Value > DateTime.UtcNow)
                {
                    await Clients.Caller.SendAsync("ErrorMessage", $"Ваш чат тимчасово обмежено оператором безпеки до {sender.MutedUntil.Value.ToLocalTime():dd.MM.yyyy HH:mm} за порушення правил.");
                    return;
                }

                // 1. Зберігаємо повідомлення в базу даних
                var message = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Text = text,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Messages.Add(message);
                string senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Користувач";

                // 2. Створюємо сповіщення для отримувача
                var notification = new Notification
                {
                    UserId = receiverId,
                    Title = "Нове повідомлення",
                    Message = $"Вам написав(ла) {senderName} щодо вашого оголошення.",
                    Link = $"/messages.html?seller={senderId}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // 3. Відправляємо через SignalR отримувачу (якщо онлайн)
                if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
                {
                    await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                    await Clients.Client(receiverConnectionId).SendAsync("ReceiveNotification", notification);
                }

                // Також відправляємо підтвердження відправнику
                if (_userConnections.TryGetValue(senderId, out var senderConnectionId))
                {
                    await Clients.Client(senderConnectionId).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                }

                // 4. Сповіщення в Telegram
                var receiver = await _context.Users.FindAsync(receiverId);
                if (receiver != null && receiver.TelegramChatId != 0 && receiver.ReceiveTelegramNotifications)
                {
                    string notificationText = $"💬 Нове повідомлення від {senderName}:\n\"{text}\"\n\nПерейдіть на Loop Market, щоб відповісти!";
                    await _telegramService.SendNotificationAsync(receiver.TelegramChatId, notificationText);
                }
            }
            else
            {
                Console.WriteLine($"❌ Помилка парсингу ID у SendMessage: senderIdStr='{senderIdStr}', receiverIdStr='{receiverIdStr}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ПОМИЛКА В SendMessage: {ex.Message} | StackTrace: {ex.StackTrace}");
        }
    }
}
