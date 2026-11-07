using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;

namespace TradeStorm.Api.Services;

public class TelegramLoginSession
{
    public string Code { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TelegramBotService : BackgroundService
{
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TelegramLoginSession> LoginSessions = new();

    private readonly TelegramBotClient? _botClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramBotService(IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var token = config["Telegram:BotToken"] ?? config["TelegramBot:Token"];
        if (string.IsNullOrWhiteSpace(token) || token.Contains("YOUR_"))
        {
            token = "8990313504:AAGUpi64ddFbrAEa8KxR97lJJ2Y-1kz14uY";
        }

        try
        {
            _botClient = new TelegramBotClient(token);
            Console.WriteLine("[Telegram Info] TelegramBotClient успішно ініціалізовано з токеном.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telegram Warning] Не вдалося ініціалізувати бота: {ex.Message}");
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

        // Тримаємо фоновий процес активним протягом усього часу роботи сервера
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful stop
        }
    }

    public async Task SendNotificationAsync(long telegramChatId, string message)
    {
        if (_botClient == null) return;

        try
        {
            await _botClient.SendMessage(
                chatId: telegramChatId,
                text: message,
                parseMode: ParseMode.Html
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telegram Warning] HTML parse failed: {ex.Message}. Falling back to plain text.");
            try
            {
                var plainText = System.Text.RegularExpressions.Regex.Replace(message, "<.*?>", string.Empty);
                await _botClient.SendMessage(
                    chatId: telegramChatId,
                    text: plainText
                );
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"❌ Помилка надсилання сповіщення в Telegram: {ex2.Message}");
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Telegram Update] Отримано оновлення типу: {update.Type}");

        if (update.Message is not { } message) return;

        Console.WriteLine($"[Telegram Message] Повідомлення від {message.Chat.Id}: {message.Text ?? message.Type.ToString()}");

                // 0. Авторизація через Telegram-бота: /start login_XYZ
        if (message.Text != null && System.Text.RegularExpressions.Regex.IsMatch(message.Text, @"^/start\s+(login_[a-zA-Z0-9_]+)$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(message.Text, @"^/start\s+(login_[a-zA-Z0-9_]+)$");
            var code = match.Groups[1].Value.Trim();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

            // Знаходимо користувача за TelegramChatId
            var targetUser = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == message.Chat.Id, cancellationToken);
            if (targetUser == null)
            {
                // Автоматична реєстрація нового облікового запису через Telegram
                var firstName = message.Chat.FirstName ?? "Користувач";
                var lastName = message.Chat.LastName ?? "Telegram";
                var email = !string.IsNullOrWhiteSpace(message.Chat.Username)
                    ? $"{message.Chat.Username}@t.me"
                    : $"tg_{message.Chat.Id}@loopmarket.ua";

                targetUser = new TradeStorm.Api.Models.User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    TelegramChatId = message.Chat.Id,
                    IsPhoneVerified = true,
                    Role = TradeStorm.Api.Models.UserRoles.Buyer,
                    PasswordHash = "TELEGRAM_BOT_LOGIN",
                    ReceiveTelegramNotifications = true
                };
                db.Users.Add(targetUser);
                await db.SaveChangesAsync(cancellationToken);
            }

            var accessToken = tokenService.GenerateAccessToken(targetUser);
            var refreshToken = tokenService.GenerateRefreshToken();
            targetUser.RefreshToken = refreshToken;
            targetUser.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync(cancellationToken);

            // Підтверджуємо сесію для клієнта
            if (LoginSessions.TryGetValue(code, out var session))
            {
                session.IsApproved = true;
                session.Token = accessToken;
                session.RefreshToken = refreshToken;
                session.UserEmail = targetUser.Email;
                session.UserName = $"{targetUser.FirstName} {targetUser.LastName}";
                session.UserRole = targetUser.Role;
            }

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"✅ <b>Вхід підтверджено!</b>\n\nВи успішно увійшли на платформу <b>LoopMarket</b> як <b>{targetUser.FirstName} {targetUser.LastName}</b>.\nБраузер автоматично відкриє ваш профіль.",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        // 1. Коли користувач надсилає команду з прив'язкою акаунту /start verify_123
        if (message.Text != null && System.Text.RegularExpressions.Regex.IsMatch(message.Text, @"^/start\s+(?:verify_)?(\d+)$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(message.Text, @"^/start\s+(?:verify_)?(\d+)$");
            if (int.TryParse(match.Groups[1].Value, out int userId))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var targetUser = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (targetUser != null)
                {
                    // 🛡️ Гарантуємо унікальність: 1 Telegram Chat ID належить виключно 1 акаунту
                    var oldUsers = await db.Users.Where(u => u.TelegramChatId == message.Chat.Id && u.Id != targetUser.Id).ToListAsync(cancellationToken);
                    foreach (var ou in oldUsers)
                    {
                        ou.TelegramChatId = 0;
                    }

                    targetUser.IsPhoneVerified = true;
                    targetUser.TelegramChatId = message.Chat.Id;
                    targetUser.ReceiveTelegramNotifications = true;
                    await db.SaveChangesAsync(cancellationToken);

                    var replyKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new[] { new KeyboardButton("📦 Мої замовлення"), new KeyboardButton("🔔 Сповіщення: Увімкнено ✅") },
                        new[] { KeyboardButton.WithRequestContact("📱 Оновити мій номер") }
                    }) { ResizeKeyboard = true };

                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"🎉 <b>Вітаємо, {targetUser.FirstName}!</b>\n\n✅ <b>Номер підтверджено та акаунт успішно підключено до Telegram!</b>\n🔔 <b>Сповіщення активовано:</b> тепер ви будете отримувати повідомлення про замовлення, доставку посилок та чат.",
                        parseMode: ParseMode.Html,
                        replyMarkup: replyKeyboard,
                        cancellationToken: cancellationToken);
                    return;
                }
            }
        }

        // 2. Коли користувач пише /start або будь-яке повідомлення у чат
        if ((message.Text != null && message.Text.StartsWith("/start")) || (message.Text != null && message.Type == MessageType.Text))
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                KeyboardButton.WithRequestContact("📱 Надіслати мій номер (Підтвердити)")
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "👋 <b>Вітаємо в Loop Market!</b>\n\nДля безпечного підтвердження номера телефону та налаштування сповіщень натисніть кнопку нижче («📱 Надіслати мій номер»).",
                parseMode: ParseMode.Html,
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
                // 🛡️ Гарантуємо унікальність: відв'язуємо цей ChatId від будь-яких інших номерів
                var oldUsers = await db.Users.Where(u => u.TelegramChatId == message.Chat.Id && u.Id != user.Id).ToListAsync(cancellationToken);
                foreach (var ou in oldUsers)
                {
                    ou.TelegramChatId = 0;
                }

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
