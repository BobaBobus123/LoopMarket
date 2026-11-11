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
    private readonly IBackgroundTaskQueue? _taskQueue;
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _userConnections = new();

    public ChatHub(AppDbContext context, TelegramBotService telegramService, IBackgroundTaskQueue? taskQueue = null)
    {
        _context = context;
        _telegramService = telegramService;
        _taskQueue = taskQueue;
    }

    private static void AddConnection(int userId, string connectionId)
    {
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        set[connectionId] = 0;
    }

    public static IReadOnlyList<string> GetConnectionsForUser(int userId)
    {
        if (_userConnections.TryGetValue(userId, out var set) && !set.IsEmpty)
        {
            return set.Keys.ToList();
        }
        return Array.Empty<string>();
    }

    public override Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            AddConnection(userId, Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public Task RegisterUser(int userId)
    {
        var authUserIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(authUserIdStr, out int authUserId) && authUserId != userId)
        {
            userId = authUserId;
        }

        AddConnection(userId, Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var pair in _userConnections)
        {
            if (pair.Value.TryRemove(Context.ConnectionId, out _) && pair.Value.IsEmpty)
            {
                _userConnections.TryRemove(new KeyValuePair<int, ConcurrentDictionary<string, byte>>(pair.Key, pair.Value));
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
            var connections = GetConnectionsForUser(receiverId);
            if (connections.Count > 0)
            {
                await Clients.Clients(connections).SendAsync("UserIsTyping", senderIdStr);
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

                // 🛡️ АНТИШАХРАЙСЬКИЙ ФІЛЬТР: Перевірка на фішинг, підроблені сайти оплат та скорочувачі посилань
                if (PhishingGuard.IsPhishingOrScam(text, out var scamReason))
                {
                    Console.WriteLine($"🚨 [Антифрод] Заблоковано фішингове повідомлення від ID {senderId}: '{text}' ({scamReason})");

                    // 1. Попередження відправнику
                    await Clients.Caller.SendAsync("ErrorMessage", $"🛡️ Повідомлення заблоковано системою безпеки Loop Захист! {scamReason}. Для вашої безпеки та безпеки співрозмовника надсилання зовнішніх платіжних посилань суворо заборонено.");

                    // 2. Фіксація скарги на шахрайство
                    var autoComplaint = new Complaint
                    {
                        ReporterId = receiverId,
                        TargetUserId = senderId,
                        Reason = "Автоматичний захист: спроба фішингу або підозріле платіжне посилання",
                        Details = $"Виявлена загроза: {scamReason}\nТекст: {text}",
                        Status = ComplaintStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Complaints.Add(autoComplaint);

                    // 3. Запис в журнал аудиту безпеки
                    var auditLog = new AdminAuditLog
                    {
                        AdminId = 1,
                        AdminEmail = "security-guard@loopmarket.ua",
                        Action = "ANTIFRAUD_PHISHING_BLOCKED",
                        TargetType = "User",
                        TargetId = senderId,
                        Details = $"Спроба надсилання фішингу користувачу {receiverId}. Причина: {scamReason}. Текст: {text}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AdminAuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
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

                // 3. Відправляємо через SignalR на всі відкриті вкладки/пристрої отримувача та відправника
                var receiverConnections = GetConnectionsForUser(receiverId);
                if (receiverConnections.Count > 0)
                {
                    await Clients.Clients(receiverConnections).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                    await Clients.Clients(receiverConnections).SendAsync("ReceiveNotification", notification);
                }

                var senderConnections = GetConnectionsForUser(senderId);
                if (senderConnections.Count > 0)
                {
                    await Clients.Clients(senderConnections).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                }

                // 4. Асинхронне сповіщення в Telegram (через чергу, щоб не блокувати SignalR)
                var receiver = await _context.Users.AsNoTracking()
                    .Where(u => u.Id == receiverId)
                    .Select(u => new { u.TelegramChatId, u.ReceiveTelegramNotifications })
                    .FirstOrDefaultAsync();

                if (receiver != null && receiver.TelegramChatId != 0 && receiver.ReceiveTelegramNotifications)
                {
                    var senderTgId = sender?.TelegramChatId ?? await _context.Users.AsNoTracking()
                        .Where(u => u.Id == senderId)
                        .Select(u => u.TelegramChatId)
                        .FirstOrDefaultAsync();

                    if (senderTgId != receiver.TelegramChatId)
                    {
                        var targetChatId = receiver.TelegramChatId;
                        string notificationText = $"💬 Нове повідомлення від {senderName}:\n\"{text}\"\n\nПерейдіть на Loop Market, щоб відповісти!";
                        if (_taskQueue != null)
                        {
                            await _taskQueue.QueueBackgroundWorkItemAsync(async _ =>
                                await _telegramService.SendNotificationAsync(targetChatId, notificationText));
                        }
                        else
                        {
                            await _telegramService.SendNotificationAsync(targetChatId, notificationText);
                        }
                    }
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
