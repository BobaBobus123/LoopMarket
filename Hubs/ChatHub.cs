using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly ConcurrentDictionary<int, string> _userConnections = new();

        public ChatHub(AppDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
        }

        public Task RegisterUser(int userId)
        {
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
            Console.WriteLine($"🔥 ВИКЛИКАНО SendMessage У ХАБІ! Від: {senderIdStr}, Кому: {receiverIdStr}, Текст: {text}");

            try
            {
                if (int.TryParse(senderIdStr, out int senderId) && int.TryParse(receiverIdStr, out int receiverId))
                {
                    // 1. Сохраняем сообщение в базу данных
                    var message = new Message
                    {
                        SenderId = senderId,
                        ReceiverId = receiverId,
                        Text = text,
                        SentAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(message);

                    // Получаем имена для красивого уведомления
                    var sender = await _context.Users.FindAsync(senderId);
                    string senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Користувач";

                    // 2. Создаем уведомление для получателя в базе данных
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
                    Console.WriteLine("💾 Повідомлення та сповіщення успішно збережено в БД з хабу.");

                    // 3. Отправляем через SignalR тем, кто онлайн
                    if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
                    {
                        await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                        // Отправляем событие для обновления центра уведомлений в реальном времени
                        await Clients.Client(receiverConnectionId).SendAsync("ReceiveNotification", notification);
                    }

                    if (_userConnections.TryGetValue(senderId, out var senderConnectionId))
                    {
                        await Clients.Client(senderConnectionId).SendAsync("ReceiveMessage", senderIdStr, receiverIdStr, text);
                    }

                    // 4. Отправка Telegram-уведомления
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var telegramService = scope.ServiceProvider.GetService<TelegramBotService>();

                    if (telegramService != null)
                    {
                        var receiver = await db.Users.FindAsync(receiverId);

                        if (receiver != null)
                        {
                            Console.WriteLine($"🔍 ДАНІ ОТРИМУВАЧА З БД: ID={receiverId}, TelegramChatId={receiver.TelegramChatId}, Notif={receiver.ReceiveTelegramNotifications}");

                            if (receiver.TelegramChatId != 0 && receiver.ReceiveTelegramNotifications)
                            {
                                string notificationText = $"💬 Нове повідомлення від {senderName}:\n\"{text}\"\n\nПерейдіть на Loop Market, щоб відповісти!";

                                await telegramService.SendNotificationAsync(receiver.TelegramChatId, notificationText);
                                Console.WriteLine($"📨 Telegram-сповіщення УСПІШНО відправлено на ChatId: {receiver.TelegramChatId}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ [Telegram Skip] У отримувача ID {receiverId} умова не виконана (ChatId дорівнює нулю або вимкнені сповіщення).");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"❌ Отримувача з ID {receiverId} не знайдено в базі даних!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Сервіс TelegramBotService не знайдено в контейнері залежностей!");
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
}