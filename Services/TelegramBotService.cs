using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;

namespace TradeStorm.Api.Services;

public class TelegramBotService : BackgroundService
{
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramBotService(IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var token = config["Telegram:BotToken"] ?? config["TelegramBot:Token"];
        if (!string.IsNullOrWhiteSpace(token) && token != "YOUR_BOT_TOKEN_HERE")
        {
            try
            {
                _botClient = new TelegramBotClient(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telegram Warning] Не вдалося ініціалізувати бота: {ex.Message}");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null)
        {
            Console.WriteLine("[Telegram Info] Telegram токен не налаштовано або не вказано. Сервіс бота не запущено.");
            return;
        }

        try
        {
            // Скидаємо старий вебхук для коректної роботи Polling
            await _botClient.DeleteWebhook(cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telegram Warning] Не вдалося видалити вебхук: {ex.Message}");
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Слухаємо всі оновлення
        };

        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, stoppingToken);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Telegram] Бот успішно запущений і слухає повідомлення!\n");
        Console.ResetColor();
    }

    public async Task SendNotificationAsync(long telegramChatId, string message)
    {
        if (_botClient == null) return;

        try
        {
            await _botClient.SendMessage(
                chatId: telegramChatId,
                text: message
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка надсилання сповіщення в Telegram: {ex.Message}");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Telegram Update] Отримано оновлення типу: {update.Type}");

        if (update.Message is not { } message) return;

        Console.WriteLine($"[Telegram Message] Повідомлення від {message.Chat.Id}: {message.Text ?? message.Type.ToString()}");

        // 1. Коли користувач пише /start або будь-яке повідомлення у чат
        if ((message.Text != null && message.Text.StartsWith("/start")) || (message.Text != null && message.Type == MessageType.Text))
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                KeyboardButton.WithRequestContact("📱 Надіслати мій номер")
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Вітаємо в Loop Market! Для підтвердження та прив'язки акаунту натисніть кнопку нижче («📱 Надіслати мій номер»).",
                replyMarkup: replyKeyboardMarkup,
                cancellationToken: cancellationToken);
        }
        // 2. Коли користувач натиснув кнопку "Надіслати мій номер"
        else if (message.Type == MessageType.Contact && message.Contact != null)
        {
            var phone = message.Contact.PhoneNumber;

            if (message.Contact.UserId != message.From?.Id)
            {
                await botClient.SendMessage(message.Chat.Id, "❌ Виявлено невідповідність! Ви надіслали чужий номер.", cancellationToken: cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cleanTelegramPhone = new string(phone.Where(char.IsDigit).ToArray());

            // Спершу шукаємо прямим запитом у БД
            var user = await db.Users
                .Where(u => u.PhoneNumber == cleanTelegramPhone || u.PhoneNumber == phone)
                .OrderByDescending(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Якщо не знайшли (через форматування в БД), перевіряємо
            if (user == null)
            {
                var candidates = await db.Users.Where(u => u.PhoneNumber != null).ToListAsync(cancellationToken);
                user = candidates.FirstOrDefault(u => new string(u.PhoneNumber!.Where(char.IsDigit).ToArray()) == cleanTelegramPhone);
            }

            if (user != null)
            {
                user.IsPhoneVerified = true;
                user.TelegramChatId = message.Chat.Id;
                user.ReceiveTelegramNotifications = true;
                await db.SaveChangesAsync(cancellationToken);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "✅ Ваш номер успішно підтверджено та акаунт прив'язано до Loop Market! Тепер ви отримуватимете сюди сповіщення.",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);

                Console.WriteLine($"[Telegram Success] Користувача ID {user.Id} успішно прив'язано до Telegram ChatId: {message.Chat.Id}");
            }
            else
            {
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Акаунт з таким номером не знайдено в базі Loop Market! Переконайтеся, що ви вказали правильний номер під час реєстрації на сайті.",
                    cancellationToken: cancellationToken);

                Console.WriteLine($"[Telegram Error] Номер {cleanTelegramPhone} не знайдено в базі.");
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Помилка Telegram Bot API: {exception.Message}");
        return Task.CompletedTask;
    }
}
