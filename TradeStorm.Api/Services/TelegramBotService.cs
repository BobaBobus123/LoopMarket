using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;

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
    public static long MasterSupportChatId { get; set; } = 0;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, List<DateTime>> RateLimitTracker = new();
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<long, bool> UserSupportState = new();
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<long, bool> AdminBroadcastState = new();
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<long, int> AdminReplyingToTicketState = new();

    private readonly TelegramBotClient? _botClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramBotService(IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") 
            ?? config["Telegram:BotToken"] 
            ?? config["TelegramBot:Token"];

        try
        {
            if (!string.IsNullOrWhiteSpace(token)) _botClient = new TelegramBotClient(token);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
                    AllowedUpdates = Array.Empty<UpdateType>() // Слухаємо всі типи оновлень (Message + CallbackQuery)
                };

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, linkedCts.Token);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[Telegram] Бот успішно запущений і слухає повідомлення!\n");
                Console.ResetColor();

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Помилка в циклі Telegram бота: {ex.Message}. Перезапуск polling через 5 секунд...");
                try { await Task.Delay(5000, stoppingToken); } catch { break; }
            }
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
        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
                return;
            }

            if (update.Message is not { } message) return;

            await HandleMessageAsync(botClient, message, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [Telegram Update Error]: {ex.Message}");
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken cancellationToken)
    {
        var chatId = callback.Message?.Chat.Id ?? callback.From.Id;
        var data = callback.Data ?? string.Empty;

        try
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
        }
        catch { }

        // 🛡️ АДМІН-КОЛБЕКИ (ВИРІШЕННЯ ТІКЕТІВ ТА БАН)
        if (data.StartsWith("adm_rep_") && int.TryParse(data.Substring(8), out int repId))
        {
            AdminReplyingToTicketState[chatId] = repId;
            await botClient.AnswerCallbackQuery(callback.Id, "Введіть текст офіційної відповіді", cancellationToken: cancellationToken);
            await botClient.SendMessage(
                chatId: chatId,
                text: $"✍️ <b>Офіційна відповідь служби підтримки по тікету #{repId}</b>\n\n" +
                      "Введіть повідомлення наступним повідомленням у цей чат.\n\n" +
                      "Воно надійде користувачеві у внутрішній чат сайту та офіційний Telegram-бот платформи без розкриття ваших особистих контактів.\n\n" +
                      "<i>Для скасування надішліть /cancel або натисніть «❌ Скасувати розсилку»</i>",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminCancelKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        if (data.StartsWith("adm_res_") && int.TryParse(data.Substring(8), out int resId))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var complaint = await db.Complaints.Include(c => c.Reporter).FirstOrDefaultAsync(c => c.Id == resId, cancellationToken);
            if (complaint != null)
            {
                complaint.Status = ComplaintStatus.Resolved;
                complaint.ResolutionNotes = "Вирішено адміністратором через Telegram @LoopMarketSupport";
                await db.SaveChangesAsync(cancellationToken);

                try { await botClient.AnswerCallbackQuery(callback.Id, "✅ Тікет успішно вирішено!", cancellationToken: cancellationToken); } catch {}
                if (callback.Message != null)
                {
                    try
                    {
                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: callback.Message.MessageId,
                            text: (callback.Message.Text ?? "") + "\n\n━━━━━━━━━━━━━━━━━━\n✅ <b>ВИРІШЕНО АДМІНІСТРАТОРОМ @LoopMarketSupport</b>",
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                    } catch {}
                }

                if (complaint.Reporter?.TelegramChatId != null && complaint.Reporter.TelegramChatId != 0 && complaint.Reporter.TelegramChatId != chatId)
                {
                    try
                    {
                        await botClient.SendMessage(
                            chatId: complaint.Reporter.TelegramChatId,
                            text: $"✅ <b>Ваше звернення {complaint.Reason} успішно розглянуто та вирішено адміністратором!</b>\n\nДякуємо за звернення до Loop Market.",
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                    } catch {}
                }
            }
            return;
        }

        if (data.StartsWith("adm_dsm_") && int.TryParse(data.Substring(8), out int dsmId))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var complaint = await db.Complaints.FindAsync(new object[] { dsmId }, cancellationToken);
            if (complaint != null)
            {
                complaint.Status = ComplaintStatus.Dismissed;
                complaint.ResolutionNotes = "Відхилено адміністратором через Telegram";
                await db.SaveChangesAsync(cancellationToken);

                try { await botClient.AnswerCallbackQuery(callback.Id, "❌ Тікет відхилено", cancellationToken: cancellationToken); } catch {}
                if (callback.Message != null)
                {
                    try
                    {
                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: callback.Message.MessageId,
                            text: (callback.Message.Text ?? "") + "\n\n━━━━━━━━━━━━━━━━━━\n❌ <b>ВІДХИЛЕНО АДМІНІСТРАТОРОМ</b>",
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                    } catch {}
                }
            }
            return;
        }

        if (data.StartsWith("adm_ban_") && int.TryParse(data.Substring(8), out int banId))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var targetUser = await db.Users.FindAsync(new object[] { banId }, cancellationToken);
            if (targetUser != null)
            {
                targetUser.IsBanned = true;
                await db.SaveChangesAsync(cancellationToken);

                try { await botClient.AnswerCallbackQuery(callback.Id, $"🚫 Користувача {targetUser.FirstName} заблоковано!", cancellationToken: cancellationToken); } catch {}
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"🚫 <b>Користувача #{targetUser.Id} ({targetUser.FirstName} {targetUser.LastName}, {targetUser.Email}) успішно ЗАБЛОКОВАНО!</b>",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
            }
            return;
        }

        if (data == "adm_tickets")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await HandleAdminMessageAsync(botClient, callback.Message ?? new Telegram.Bot.Types.Message(), "/tickets", "/tickets", chatId, db, null, cancellationToken);
            return;
        }

        switch (data)
        {
            case "faq_orders":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "📦 <b>Як безпечно замовити товар на Loop Market:</b>\n\n" +
                          "1️⃣ <b>Оберіть товар</b> у каталозі <code>https://loopmarket-x0uo.onrender.com</code> та натисніть «⚡ Купити».\n" +
                          "2️⃣ <b>Вкажіть дані доставки:</b> ваше місто та зручне відділення Нової Пошти.\n" +
                          "3️⃣ <b>Безпечна оплата:</b> за замовчуванням діє <b>Накладений платіж</b> (оплата здійснюється виключно після фізичного огляду у відділенні).\n" +
                          "4️⃣ <b>Відстеження:</b> статус та номер ТТН миттєво з'являються у вашому кабінеті та надсилаються сюди в Telegram-бот!\n\n" +
                          "<i>Ніколи не переводьте передоплату стороннім особам поза офіційним оформленням на сайті.</i>",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetInlineFaqNavigation(),
                    cancellationToken: cancellationToken);
                break;

            case "faq_services":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🛠 <b>Як замовити послугу майстра на Loop Market:</b>\n\n" +
                          "1️⃣ <b>Оберіть майстра</b> у розділі послуг (ремонт, сантехніка, електрика, клінінг тощо).\n" +
                          "2️⃣ <b>Залиште заявку:</b> натисніть «🛠 Замовити послугу», вкажіть вашу адресу/локацію, опис проблеми або кількість годин.\n" +
                          "3️⃣ <b>Чат та узгодження:</b> майстер отримує сповіщення, зв'язується з вами у чаті платформи та підтверджує візит.\n" +
                          "4️⃣ <b>Оплата:</b> ви сплачуєте кошти тільки після успішного виконання та перевірки якості роботи!\n\n" +
                          "<i>Завжди фіксуйте деталі домовленостей у чаті сайту для дії програми захисту Loop Market.</i>",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetInlineFaqNavigation(),
                    cancellationToken: cancellationToken);
                break;

            case "faq_safety":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🛡️ <b>ПРАВИЛА БЕЗПЕКИ ТА АНТИ-ШАХРАЙ ЗАХИСТ:</b>\n\n" +
                          "🚨 <b>Ознаки того, що вам пише шахрай:</b>\n" +
                          "• Просить перейти у сторонній месенджер (Viber, WhatsApp, особистий Telegram) для 'безпечної оплати'.\n" +
                          "• Надсилає сторонні посилання начебто від Нової Пошти чи банку (наприклад: <i>novaposhta-pay.online</i>, <i>secure-market.site</i> тощо).\n" +
                          "• Просить назвати: <b>CVV/CVC-код (3 цифри на звороті), термін дії або баланс картки</b>.\n\n" +
                          "🔒 <b>Золоті правила безпеки Loop Market:</b>\n" +
                          "✅ Справжній Loop Market <b>НІКОЛИ</b> не просить вводити CVV чи залишок коштів на картці!\n" +
                          "✅ Оплата при отриманні — це ваш найнадійніший захист. Перевіряйте вміст посилки перед сплатою!\n" +
                          "✅ Ведіть діалоги тільки через офіційний сайт <code>https://loopmarket-x0uo.onrender.com</code>.\n\n" +
                          "<i>Якщо натрапили на підозрілого співрозмовника — напишіть адміністратору: @LoopMarketSupport.</i>",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetInlineFaqNavigation(),
                    cancellationToken: cancellationToken);
                break;

            case "btn_check_order":
            case "faq_check_order":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🔍 <b>Перевірка статусу замовлення:</b>\n\n" +
                          "Надішліть просто 6-значний номер замовлення у чат, наприклад:\n" +
                          "👉 <code>749281</code>\n" +
                          "👉 або з префіксом: <code>LP-749281</code>\n\n" +
                          "<i>(Старі короткі номери скасовано з міркувань безпеки).</i>",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                break;

            case "btn_start_support":
            case "support_contact":
                UserSupportState[chatId] = true;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "✍️ <b>РЕЖИМ ЗВЕРНЕННЯ ДО ПІДТРИМКИ (@LoopMarketSupport)</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "Ви перейшли у форму прямого зв'язку зі службою підтримки Loop Market.\n\n" +
                          "📝 <b>Надішліть ваше запитання або опис проблеми наступним повідомленням у цей чат.</b>\n" +
                          "Воно буде зафіксовано в системі та передано черговому адміністратору (<b>@LoopMarketSupport</b>).\n\n" +
                          "<i>Якщо ви передумали — натисніть кнопку «❌ Скасувати звернення» нижче:</i>",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetSupportCancelKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            case "btn_cancel_support":
                UserSupportState.TryRemove(chatId, out _);
                using (var cancelScope = _scopeFactory.CreateScope())
                {
                    var cancelDb = cancelScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var u = await cancelDb.Users.FirstOrDefaultAsync(x => x.TelegramChatId == chatId, cancellationToken);
                    var isV = u != null && u.IsPhoneVerified;
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ <b>Звернення скасовано.</b> Ви повернулися до головного меню.",
                        parseMode: ParseMode.Html,
                        replyMarkup: GetUserKeyboard(isV, u?.ReceiveTelegramNotifications ?? true),
                        cancellationToken: cancellationToken);
                }
                break;

            default:
                break;
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Message message, CancellationToken cancellationToken)
    {
        var rawText = message.Text?.Trim() ?? string.Empty;
        var lowerText = rawText.ToLowerInvariant();
        var chatId = message.Chat.Id;

        // Автоматична реєстрація чату головного адміністратора @LoopMarketSupport
        if (message.Chat.Username?.Equals("LoopMarketSupport", StringComparison.OrdinalIgnoreCase) == true)
        {
            MasterSupportChatId = chatId;
        }

        // Перевіряємо, чи є користувач адміністратором
        using (var authScope = _scopeFactory.CreateScope())
        {
            var authDb = authScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var authUser = await authDb.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            bool isAdminChat = (message.Chat.Username?.Equals("LoopMarketSupport", StringComparison.OrdinalIgnoreCase) == true)
                               || (MasterSupportChatId != 0 && MasterSupportChatId == chatId)
                               || (authUser != null && authUser.Role == UserRoles.Admin);

            // Якщо пише адміністратор — обслуговуємо виключно його адмін-функціями (без клієнтських меню)
            if (isAdminChat)
            {
                await HandleAdminMessageAsync(botClient, message, rawText, lowerText, chatId, authDb, authUser, cancellationToken);
                return;
            }
        }

        // 0. Авторизація через Telegram-бота: /start login_XYZ
        if (System.Text.RegularExpressions.Regex.IsMatch(rawText, @"^/start\s+(login_[a-zA-Z0-9_]+)$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(rawText, @"^/start\s+(login_[a-zA-Z0-9_]+)$");
            var code = match.Groups[1].Value.Trim();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

            var targetUser = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            if (targetUser == null)
            {
                var firstName = message.Chat.FirstName ?? "Користувач";
                var lastName = message.Chat.LastName ?? "Telegram";
                var email = !string.IsNullOrWhiteSpace(message.Chat.Username)
                    ? $"{message.Chat.Username}@t.me"
                    : $"tg_{chatId}@loopmarket.ua";

                targetUser = new TradeStorm.Api.Models.User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    TelegramChatId = chatId,
                    IsPhoneVerified = true,
                    Role = UserRoles.Buyer,
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
                chatId: chatId,
                text: $"✅ <b>Вхід підтверджено!</b>\n\nВи успішно увійшли на платформу <b>Loop Market</b> як <b>{targetUser.FirstName} {targetUser.LastName}</b>.\nБраузер автоматично відкриє ваш профіль.",
                parseMode: ParseMode.Html,
                replyMarkup: GetUserKeyboard(true, targetUser.ReceiveTelegramNotifications),
                cancellationToken: cancellationToken);
            return;
        }

        // 1. Прив'язка акаунта через /start verify_123
        if (System.Text.RegularExpressions.Regex.IsMatch(rawText, @"^/start\s+(?:verify_)?(\d+)$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(rawText, @"^/start\s+(?:verify_)?(\d+)$");
            if (int.TryParse(match.Groups[1].Value, out int userId))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var targetUser = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (targetUser != null)
                {
                    var oldUsers = await db.Users.Where(u => u.TelegramChatId == chatId && u.Id != targetUser.Id).ToListAsync(cancellationToken);
                    foreach (var ou in oldUsers)
                    {
                        ou.TelegramChatId = 0;
                    }

                    targetUser.IsPhoneVerified = true;
                    targetUser.TelegramChatId = chatId;
                    targetUser.ReceiveTelegramNotifications = true;
                    await db.SaveChangesAsync(cancellationToken);

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"🎉 <b>Вітаємо, {targetUser.FirstName}!</b>\n\n" +
                              $"✅ <b>Номер підтверджено та акаунт успішно підключено до Telegram!</b>\n" +
                              $"🔔 <b>Сповіщення активовано:</b> тепер ви будете отримувати повідомлення про нові замовлення, доставку посилок та повідомлення в чаті.",
                        parseMode: ParseMode.Html,
                        replyMarkup: GetUserKeyboard(true, true),
                        cancellationToken: cancellationToken);
                    return;
                }
            }
        }

        // 2. Обробка передачі номера телефону (Contact)
        if (message.Type == MessageType.Contact && message.Contact != null)
        {
            var phone = message.Contact.PhoneNumber;

            if (message.Contact.UserId != message.From?.Id)
            {
                await botClient.SendMessage(chatId, "❌ Виявлено невідповідність! Ви надіслали чужий контакт.", cancellationToken: cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cleanTelegramPhone = new string(phone.Where(char.IsDigit).ToArray());

            var user = await db.Users
                .Where(u => u.PhoneNumber == cleanTelegramPhone || u.PhoneNumber == phone)
                .OrderByDescending(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                var candidates = await db.Users.Where(u => u.PhoneNumber != null).ToListAsync(cancellationToken);
                user = candidates.FirstOrDefault(u => new string(u.PhoneNumber!.Where(char.IsDigit).ToArray()) == cleanTelegramPhone);
            }

            if (user != null)
            {
                var oldUsers = await db.Users.Where(u => u.TelegramChatId == chatId && u.Id != user.Id).ToListAsync(cancellationToken);
                foreach (var ou in oldUsers)
                {
                    ou.TelegramChatId = 0;
                }

                user.IsPhoneVerified = true;
                user.TelegramChatId = chatId;
                user.ReceiveTelegramNotifications = true;
                await db.SaveChangesAsync(cancellationToken);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"✅ <b>Вітаємо, {user.FirstName}!</b>\n\nНомер успішно підтверджено, а ваш акаунт прив'язано до Loop Market! Сповіщення активовано.",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetUserKeyboard(true, true),
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ <b>Акаунт з таким номером не знайдено на Loop Market!</b>\n\n" +
                          $"Переконайтеся, що ви зареєструвалися на сайті під цим же номером: <code>+{cleanTelegramPhone}</code>.",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetUserKeyboard(false),
                    cancellationToken: cancellationToken);
            }
            return;
        }

        // 3. ❌ СКАСУВАННЯ РЕЖИМУ ПІДТРИМКИ АБО ПОВЕРНЕННЯ В МЕНЮ
        if (rawText.Equals("❌ Скасувати звернення", StringComparison.OrdinalIgnoreCase) ||
            rawText.Equals("« Назад у головне меню", StringComparison.OrdinalIgnoreCase) ||
            rawText.Equals("/cancel", StringComparison.OrdinalIgnoreCase) ||
            lowerText == "скасувати" || lowerText == "відміна")
        {
            UserSupportState.TryRemove(chatId, out _);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            var isVerified = user != null && user.IsPhoneVerified;

            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ <b>Звернення скасовано.</b> Ви повернулися до головного меню.",
                parseMode: ParseMode.Html,
                replyMarkup: GetUserKeyboard(isVerified, user?.ReceiveTelegramNotifications ?? true),
                cancellationToken: cancellationToken);
            return;
        }

        // 4. ✍️ КОРИСТУВАЧ У РЕЖИМІ ПІДТРИМКИ — ВІДПРАВЛЯЄМО ЗВЕРНЕННЯ
        if (UserSupportState.TryGetValue(chatId, out bool inSupportMode) && inSupportMode)
        {
            if (rawText.Length < 3)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Будь ласка, опишіть ваше запитання детальніше (кілька слів) або натисніть «❌ Скасувати звернення».",
                    replyMarkup: GetSupportCancelKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            var userName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : message.Chat.FirstName ?? "Користувач Telegram";
            var userPhone = user?.PhoneNumber ?? "Не прив'язано";
            var ticketCode = $"TK-{Random.Shared.Next(100000, 999999)}";

            await NotifyAdminsAboutSupportTicketAsync(
                botClient,
                chatId,
                userName,
                userPhone,
                message.Chat.Username,
                ticketCode,
                rawText,
                cancellationToken);

            UserSupportState.TryRemove(chatId, out _);
            var isVerified = user != null && user.IsPhoneVerified;

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ <b>Ваше звернення зареєстровано в системі підтримки!</b>\n\n" +
                      $"🎫 Код тікета: <code>{ticketCode}</code>\n" +
                      $"👤 Відправник: <b>{System.Net.WebUtility.HtmlEncode(userName)}</b>\n\n" +
                      $"📩 Повідомлення передано черговому адміністратору <b>@LoopMarketSupport</b> та зареєстровано в панелі операторів.\n" +
                      $"Вам дадуть відповідь у найкоротший термін!\n\n" +
                      $"<i>Ви також можете написати черговому адміністратору напряму:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") }
                }),
                cancellationToken: cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: "Головне меню відновлено:",
                replyMarkup: GetUserKeyboard(isVerified, user?.ReceiveTelegramNotifications ?? true),
                cancellationToken: cancellationToken);
            return;
        }

        // 5. 🚨 АНТИ-ШАХРАЙ АНАЛІЗАТОР ТА БЕЗПЕКА (SCAM & PHISHING DETECTION)
        if (IsPotentialScamOrPhishing(rawText))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🚨 <b>УВАГА! ВИЯВЛЕНО ПІДОЗРІЛИЙ ТЕКСТ АБО ПОСИЛАННЯ!</b>\n\n" +
                      "⚠️ <i>Схоже на типову спробу шахрайства (фішинг або виманювання платіжних даних).</i>\n\n" +
                      "🛡️ <b>КРИТИЧНО ВАЖЛИВІ ПРАВИЛА БЕЗПЕКИ LOOP MARKET:</b>\n" +
                      "1. 🚫 <b>Ніколи не переходьте за сторонніми посиланнями</b> для 'отримання коштів' чи 'підтвердження переказу'!\n" +
                      "2. 💳 <b>Нікому не повідомляйте:</b> CVV/CVC-код (3 цифри зі звороту картки), PIN-код, термін дії чи баланс! Loop Market ніколи не запитує цю інформацію.\n" +
                      "3. 📦 Усі замовлення на платформі здійснюються через офіційний сайт <code>https://loopmarket-x0uo.onrender.com</code>.\n" +
                      "4. 🚚 Доставка працює через <b>Накладений платіж Нової Пошти</b> — ви сплачуєте лише після фізичного огляду посилки у відділенні!\n" +
                      "5. 💬 Спілкуйтеся тільки у внутрішньому чаті сайту. Відмовтеся від пропозицій переходити у Viber або WhatsApp.\n\n" +
                      "<i>Якщо вам надіслав це посилання інший користувач — надішліть звернення черговому адміністратору @LoopMarketSupport:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("💬 Зв'язатися з адміном @LoopMarketSupport", "https://t.me/LoopMarketSupport") },
                    new[] { InlineKeyboardButton.WithCallbackData("🛡️ Детальні правила безпеки", "faq_safety") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 6. ❌ ЗАХИСТ ВІД ПЕРЕБОРУ: ЗАБОРОНА СТАРИХ КОРОТКИХ НОМЕРІВ (#14, 14, 15)
        if (System.Text.RegularExpressions.Regex.IsMatch(rawText, @"^(#\d+|\b\d{1,4}\b)$"))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ <b>Старі короткі номери замовлень більше не підтримуються з міркувань безпеки.</b>\n\n" +
                      "Усі замовлення на платформі тепер захищені унікальним 6-значним номером (наприклад: <code>749281</code> або <code>LP-749281</code>), щоб запобігти перебору номерів сторонніми особами.\n\n" +
                      "Актуальний номер вашого замовлення можна переглянути в особистому кабінеті на сайті або натиснувши кнопку «📦 Мої замовлення».",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("📋 Відкрити замовлення на сайті", "https://loopmarket-x0uo.onrender.com/profile.html#orders") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 7. 🔍 БЕЗПЕЧНА ПЕРЕВІРКА ЗАМОВЛЕННЯ ЗА 6-ЗНАЧНИМ КОДОМ
        string? extractedOrderCode = ExtractOrderCodeCandidate(rawText);
        if (extractedOrderCode != null)
        {
            // Захист від брутфорсу (перебору номерів): максимум 6 перевірок за хвилину
            if (IsRateLimited(chatId))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ <b>Забагато запитів перевірки замовлень!</b>\n\nЗ міркувань безпеки, будь ласка, зачекайте 1 хвилину перед наступною спробою.",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Order? foundOrder = null;
            if (extractedOrderCode.StartsWith("LP-", StringComparison.OrdinalIgnoreCase))
            {
                foundOrder = await db.Orders
                    .Include(o => o.Listing)
                    .Include(o => o.Seller)
                    .Include(o => o.Buyer)
                    .FirstOrDefaultAsync(o => o.OrderCode == extractedOrderCode, cancellationToken);

                if (foundOrder == null)
                {
                    var recentOrders = await db.Orders
                        .Include(o => o.Listing)
                        .Include(o => o.Seller)
                        .Include(o => o.Buyer)
                        .OrderByDescending(o => o.CreatedAt)
                        .Take(100)
                        .ToListAsync(cancellationToken);
                    foundOrder = recentOrders.FirstOrDefault(o => o.GetDisplayCode().Equals(extractedOrderCode, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (foundOrder != null)
            {
                var dispCode = foundOrder.GetDisplayCode();
                var isService = foundOrder.Listing?.CategoryId == 8 || (foundOrder.Listing?.Title ?? "").Contains("Послуг", StringComparison.OrdinalIgnoreCase);

                // 🛡️ ЗАХИСТ КОНФІДЕНЦІЙНОСТІ ТА БЕЗПЕКИ:
                // Перевіряємо, чи користувач Telegram є покупцем, продавцем або адміном цього замовлення.
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
                bool isAuthorized = (currentUser != null && (currentUser.Role == UserRoles.Admin || currentUser.Id == foundOrder.BuyerId || currentUser.Id == foundOrder.SellerId))
                                    || (foundOrder.Buyer?.TelegramChatId == chatId)
                                    || (foundOrder.Seller?.TelegramChatId == chatId);

                var statusUk = foundOrder.Status switch
                {
                    OrderStatus.Pending => "⏳ Очікує підтвердження продавцем",
                    OrderStatus.Confirmed => "✅ Підтверджено продавцем (готується до відправки)",
                    OrderStatus.Shipped => "🚚 Відправлено Новою Поштою",
                    OrderStatus.Completed => "🎉 Успішно завершено",
                    OrderStatus.Cancelled => "❌ Скасовано",
                    _ => foundOrder.Status
                };

                var statusTip = foundOrder.Status switch
                {
                    OrderStatus.Pending => "<i>Продавець отримав сповіщення про замовлення та незабаром його підтвердить.</i>",
                    OrderStatus.Confirmed => "<i>Замовлення підтверджено продавцем. Очікуйте на номер накладної (ТТН).</i>",
                    OrderStatus.Shipped => "<i>Посилка вже прямує до вашого відділення! Обов'язково огляньте її перед сплатою.</i>",
                    OrderStatus.Completed => "<i>Замовлення успішно отримано та оплачено. Дякуємо, що обираєте Loop Market!</i>",
                    OrderStatus.Cancelled => "<i>Замовлення скасовано. Товар повернуто у вільний продаж.</i>",
                    _ => ""
                };

                string deliveryDetails;
                if (isAuthorized)
                {
                    deliveryDetails = $"📍 <b>Отримання:</b> {System.Net.WebUtility.HtmlEncode(foundOrder.DeliveryCity)}, {System.Net.WebUtility.HtmlEncode(foundOrder.DeliveryBranch)}\n";
                }
                else
                {
                    // Безпечне маскування точної адреси/відділення для сторонніх осіб
                    deliveryDetails = $"📍 <b>Місто:</b> {System.Net.WebUtility.HtmlEncode(foundOrder.DeliveryCity)} <i>(відділення приховано з міркувань безпеки)</i>\n";
                }

                var replyText = $"📦 <b>Інформація про замовлення {dispCode}:</b>\n" +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"🏷 <b>{(isService ? "Послуга" : "Товар")}:</b> {System.Net.WebUtility.HtmlEncode(foundOrder.Listing?.Title ?? "Оголошення")}\n" +
                                $"💰 <b>Сума:</b> {foundOrder.TotalPrice:N0} ₴\n" +
                                $"⚡ <b>Статус:</b> <b>{statusUk}</b>\n" +
                                deliveryDetails +
                                $"💳 <b>Оплата:</b> {System.Net.WebUtility.HtmlEncode(foundOrder.PaymentMethod)}\n" +
                                (!string.IsNullOrWhiteSpace(foundOrder.TrackingNumber) ? $"🚚 <b>ТТН Нової Пошти:</b> <code>{foundOrder.TrackingNumber}</code>\n" : "") +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"{statusTip}" +
                                (!isAuthorized ? "\n\n🔒 <i>Для перегляду повних персональних даних увійдіть у прив'язаний акаунт на сайті.</i>" : "");

                await botClient.SendMessage(
                    chatId: chatId,
                    text: replyText,
                    parseMode: ParseMode.Html,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithUrl("🌐 Відкрити замовлення в кабінеті", "https://loopmarket-x0uo.onrender.com/profile.html#orders") }
                    }),
                    cancellationToken: cancellationToken);
                return;
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"🔍 <b>Замовлення за номером <code>{rawText}</code> не знайдено в системі.</b>\n\n" +
                          $"Перевірте, будь ласка, правильність цифр (наприклад, <code>749281</code> або <code>LP-749281</code>).\n" +
                          $"Також ви можете скористатися кнопкою «📦 Мої замовлення» на клавіатурі нижче.",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                return;
            }
        }

        // 5. 📦 КНОПКА "МОЇ ЗАМОВЛЕННЯ"
        if (lowerText.Contains("мої замовлення") || lowerText == "/orders")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            if (user == null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "📱 <b>Ваш Telegram ще не прив'язано до акаунта Loop Market.</b>\n\n" +
                          "Натисніть кнопку нижче («📱 Надіслати мій номер»), щоб прив'язати акаунт та бачити ваші замовлення прямо тут!",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetUserKeyboard(false),
                    cancellationToken: cancellationToken);
                return;
            }

            var orders = await db.Orders
                .Include(o => o.Listing)
                .Where(o => o.BuyerId == user.Id || o.SellerId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (orders.Count == 0)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"📦 <b>{user.FirstName}, у вас поки немає замовлень.</b>\n\n" +
                          $"Перейдіть у каталог на сайті, щоб знайти цікаві товари або запропонувати свої послуги!",
                    parseMode: ParseMode.Html,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithUrl("🌐 Перейти в каталог", "https://loopmarket-x0uo.onrender.com/index.html") }
                    }),
                    cancellationToken: cancellationToken);
                return;
            }

            var msg = $"📦 <b>Останні замовлення користувача {user.FirstName}:</b>\n━━━━━━━━━━━━━━━━━━\n\n";
            foreach (var o in orders)
            {
                var isBuyer = o.BuyerId == user.Id;
                var roleBadge = isBuyer ? "🛒 Покупка" : "🏷 Продаж";
                var statusText = o.Status switch
                {
                    OrderStatus.Pending => "Очікує підтвердження",
                    OrderStatus.Confirmed => "Підтверджено",
                    OrderStatus.Shipped => "Відправлено",
                    OrderStatus.Completed => "Завершено",
                    OrderStatus.Cancelled => "Скасовано",
                    _ => o.Status
                };

                msg += $"• <b>{o.GetDisplayCode()}</b> ({roleBadge})\n" +
                       $"  Назва: <b>{System.Net.WebUtility.HtmlEncode(o.Listing?.Title ?? "Оголошення")}</b>\n" +
                       $"  Сума: <b>{o.TotalPrice:N0} ₴</b> | Статус: <b>{statusText}</b>\n" +
                       (!string.IsNullOrWhiteSpace(o.TrackingNumber) ? $"  ТТН: <code>{o.TrackingNumber}</code>\n" : "") +
                       $"\\n";
            }
            msg += $"━━━━━━━━━━━━━━━━━━\n<i>Для перегляду всіх деталей перейдіть до особистого кабінету.</i>";

            await botClient.SendMessage(
                chatId: chatId,
                text: msg,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("📋 Відкрити мої замовлення", "https://loopmarket-x0uo.onrender.com/profile.html#orders") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 6. 🛡️ КНОПКА "БЕЗПЕКА ПЛАТФОРМИ"
        if (lowerText.Contains("безпека") || lowerText == "/safety")
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🛡️ <b>ЦЕНТР БЕЗПЕКИ ТА ЗАХИСТУ ВІД ШАХРАЇВ LOOP MARKET:</b>\n\n" +
                      "Наша мета — зробити кожну угоду абсолютно надійною та прозорою.\n\n" +
                      "<b>Основні правила безпечної торгівлі:</b>\n" +
                      "1. 📦 <b>Накладений платіж:</b> найбезпечніший спосіб покупки. Ви платите за товар лише після того, як тримаєте його в руках та оглянули у відділенні Нової Пошти.\n" +
                      "2. 🚫 <b>Ніколи не переходьте за сторонніми посиланнями:</b> якщо покупець чи продавець просить 'підтвердити оплату' чи 'отримати гроші на картку' за якимось посиланням — це 100% шахраї!\n" +
                      "3. 💳 <b>Конфіденційність картки:</b> для отримання оплати на картку покупцю потрібен ТІЛЬКИ 16-значний номер. <b>Ніколи не називайте термін дії та CVV-код!</b>\n" +
                      "4. 💬 <b>Внутрішній чат:</b> домовляйтеся про деталі у чаті нашого сайту — у разі виникнення суперечок це є офіційним доказом для адміністрації.\n\n" +
                      "<i>Натисніть кнопку нижче, щоб повідомити адміністратора про підозрілі дії:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") },
                    new[] { InlineKeyboardButton.WithUrl("📖 Читати повні правила безпеки", "https://loopmarket-x0uo.onrender.com/safety.html") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 8. 🆘 ПЕРЕХІД У РЕЖИМ ПІДТРИМКИ
        if (lowerText.Contains("підтримк") || lowerText.Contains("допомог") || lowerText.Contains("help") || lowerText == "/support" || lowerText == "/help" || lowerText.Contains("оператор") || lowerText.Contains("sos"))
        {
            UserSupportState[chatId] = true;
            await botClient.SendMessage(
                chatId: chatId,
                text: "✍️ <b>РЕЖИМ ЗВЕРНЕННЯ ДО ПІДТРИМКИ (@LoopMarketSupport)</b>\n" +
                      "━━━━━━━━━━━━━━━━━━\n" +
                      "Ви перейшли у форму зв'язку з черговим адміністратором платформи Loop Market.\n\n" +
                      "📝 <b>Надішліть ваше запитання або опис проблеми наступним повідомленням у цей чат.</b>\n" +
                      "Воно буде зафіксовано в системі підтримки та надіслано черговому адміністратору (<b>@LoopMarketSupport</b>).\n\n" +
                      "<i>Якщо ви передумали або зайшли випадково — натисніть кнопку «❌ Скасувати звернення» нижче:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: GetSupportCancelKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        // 9. 🔍 КНОПКА "ПЕРЕВІРИТИ ЗАМОВЛЕННЯ"
        if (lowerText.Contains("перевірити замовлення") || lowerText == "/track")
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🔍 <b>Перевірка статусу замовлення в 1 клік:</b>\n\n" +
                      "Надішліть сюди у чат 6-значний номер вашого замовлення, наприклад:\n" +
                      "👉 просто цифри: <code>749281</code>\n" +
                      "👉 або з префіксом: <code>LP-749281</code>\n\n" +
                      "Я миттєво покажу актуальний статус посилки, місто та номер ТТН!",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        // 10. 🔔 КНОПКА ПЕРЕМИКАННЯ СПОВІЩЕНЬ
        if (lowerText.Contains("сповіщення") || lowerText == "/notifications")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            if (user != null)
            {
                user.ReceiveTelegramNotifications = !user.ReceiveTelegramNotifications;
                await db.SaveChangesAsync(cancellationToken);

                var statusText = user.ReceiveTelegramNotifications ? "увімкнено ✅" : "вимкнено ❌";
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"🔔 <b>Telegram-сповіщення успішно {statusText}!</b>\n\n" +
                          (user.ReceiveTelegramNotifications 
                              ? "Тепер ви миттєво отримуватимете інформацію про нові замовлення, повідомлення та зміну статусів посилок."
                              : "Сповіщення вимкнено. Ви можете увімкнути їх у будь-який момент назад цією ж кнопкою."),
                    parseMode: ParseMode.Html,
                    replyMarkup: GetUserKeyboard(true, user.ReceiveTelegramNotifications),
                    cancellationToken: cancellationToken);
                return;
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "📱 Спочатку прив'яжіть свій акаунт за допомогою кнопки «📱 Надіслати мій номер».",
                    replyMarkup: GetUserKeyboard(false),
                    cancellationToken: cancellationToken);
                return;
            }
        }

        // 11. 🌐 ВІДКРИТИ САЙТ
        if (lowerText.Contains("відкрити") || lowerText.Contains("сайт") || lowerText == "/site")
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🌐 <b>Ласкаво просимо на офіційну платформу Loop Market!</b>\n\n" +
                      "Купуйте та продавайте товари по всій Україні, а також замовляйте перевірені послуги майстрів.",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("🛍 Відкрити Головну каталогу", "https://loopmarket-x0uo.onrender.com/index.html") },
                    new[] { InlineKeyboardButton.WithUrl("🛠 Каталог послуг майстрів", "https://loopmarket-x0uo.onrender.com/index.html?type=service") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 12. ДЕФОЛТНЕ ПОВІДОМЛЕННЯ (КОРИСТУВАЧ НАПИСАВ ТЕКСТ ПОЗА РЕЖИМОМ ПІДТРИМКИ)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            var isVerified = user != null && user.IsPhoneVerified;

            await botClient.SendMessage(
                chatId: chatId,
                text: "🤖 <b>Вітаємо! Я офіційний чат-бот платформи Loop Market.</b>\n\n" +
                      "Чим я можу вам допомогти?\n\n" +
                      "• 📦 <b>Статус замовлення:</b> надішліть його 6-значний номер (наприклад, <code>749281</code> або <code>LP-749281</code>).\n" +
                      "• 🆘 <b>Підтримка:</b> натисніть кнопку нижче, щоб відкрити офіційне звернення до чергового адміністратора <b>@LoopMarketSupport</b>.\n" +
                      "• 🛡️ <b>Безпека:</b> дізнайтеся перевірені правила захисту від шахраїв.",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✍️ Написати в підтримку", "btn_start_support") },
                    new[] { InlineKeyboardButton.WithUrl("💬 Написати @LoopMarketSupport", "https://t.me/LoopMarketSupport") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔍 Перевірити замовлення", "btn_check_order") },
                    new[] { InlineKeyboardButton.WithCallbackData("🛡️ Правила безпеки", "faq_safety") }
                }),
                cancellationToken: cancellationToken);
        }
    }

    private static string? ExtractOrderCodeCandidate(string text)
    {
        var raw = text.Trim();

        // 1. Повний префікс LP-XXXXXX, lp XXXXXX або LPXXXXXX (5-8 символів)
        var lpMatch = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?:LP[- ]?([0-9A-Za-z_-]{5,8}))\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (lpMatch.Success)
        {
            return $"LP-{lpMatch.Groups[1].Value.ToUpperInvariant()}";
        }

        // 2. Введення просто 6 цифр (або 5-7 цифр без решітки)
        var digitsMatch = System.Text.RegularExpressions.Regex.Match(raw, @"^\s*(\d{5,7})\s*$");
        if (digitsMatch.Success)
        {
            return $"LP-{digitsMatch.Groups[1].Value}";
        }

        return null;
    }

    private static bool IsRateLimited(long chatId)
    {
        var now = DateTime.UtcNow;
        var attempts = RateLimitTracker.GetOrAdd(chatId, _ => new List<DateTime>());
        lock (attempts)
        {
            attempts.RemoveAll(t => now - t > TimeSpan.FromMinutes(1));
            if (attempts.Count >= 6)
            {
                return true;
            }
            attempts.Add(now);
            return false;
        }
    }

    private async Task NotifyAdminsAboutSupportTicketAsync(
        ITelegramBotClient botClient, 
        long userChatId, 
        string userName, 
        string userPhone, 
        string? userUsername, 
        string ticketCode, 
        string userMessageText, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var adminUsers = await db.Users.Where(u => u.Role == UserRoles.Admin).ToListAsync(cancellationToken);
            var firstAdmin = adminUsers.FirstOrDefault() ?? await db.Users.FirstOrDefaultAsync(cancellationToken);
            var reporterUser = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == userChatId, cancellationToken);

            int reporterId = reporterUser?.Id ?? (firstAdmin?.Id ?? 1);
            int targetUserId = firstAdmin?.Id ?? reporterId;

            // 1. Зберігаємо звернення як офіційний Complaint (тікет підтримки) в базі даних для адмін-панелі сайту
            var complaint = new Complaint
            {
                ReporterId = reporterId,
                TargetUserId = targetUserId,
                Reason = $"💬 Звернення підтримки [{ticketCode}]",
                Details = $"👤 Заявник: {userName}\n📱 Телефон: {userPhone}\n💬 Telegram: {(string.IsNullOrWhiteSpace(userUsername) ? "не вказано" : "@" + userUsername)} (ChatId: {userChatId})\n\n📝 Повідомлення:\n{userMessageText}",
                Status = ComplaintStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            db.Complaints.Add(complaint);

            // 2. Зберігаємо сповіщення в веб-кабінеті адміністраторів
            foreach (var adm in adminUsers)
            {
                var notif = new Notification
                {
                    UserId = adm.Id,
                    Title = $"🆘 Звернення {ticketCode} в Telegram",
                    Message = $"{userName} (тел. {userPhone}, @{userUsername ?? "немає"}): {userMessageText}",
                    Link = "/admin.html",
                    CreatedAt = DateTime.UtcNow
                };
                db.Notifications.Add(notif);
            }
            await db.SaveChangesAsync(cancellationToken);

            // 3. Відправляємо миттєвий Telegram push головному адміністратору @LoopMarketSupport та підключеним адмінам
            var adminChatIds = new HashSet<long>();
            if (MasterSupportChatId != 0 && MasterSupportChatId != userChatId)
            {
                adminChatIds.Add(MasterSupportChatId);
            }
            foreach (var adm in adminUsers.Where(a => a.TelegramChatId != 0 && a.TelegramChatId != userChatId))
            {
                adminChatIds.Add(adm.TelegramChatId);
            }

            if (adminChatIds.Count > 0)
            {
                var adminText = $"🚨 <b>НОВЕ ЗВЕРНЕННЯ В ПІДТРИМКУ!</b>\n" +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"🎫 Код тікета: <code>{ticketCode}</code>\n" +
                                $"👤 Користувач: <b>{System.Net.WebUtility.HtmlEncode(userName)}</b>\n" +
                                $"📱 Телефон: <code>{userPhone}</code>\n" +
                                (!string.IsNullOrWhiteSpace(userUsername) ? $"💬 Telegram: @{userUsername}\n" : $"🆔 ChatId: <code>{userChatId}</code>\n") +
                                $"━━━━━━━━━━━━━━━━━━\n" +
                                $"📝 <b>Повідомлення:</b>\n<i>{System.Net.WebUtility.HtmlEncode(userMessageText)}</i>";

                var replyBtns = new List<InlineKeyboardButton>();
                if (!string.IsNullOrWhiteSpace(userUsername))
                {
                    replyBtns.Add(InlineKeyboardButton.WithUrl("💬 Відповісти в Telegram", $"https://t.me/{userUsername}"));
                }

                var markup = replyBtns.Count > 0 ? new InlineKeyboardMarkup(new[] { replyBtns.ToArray() }) : null;

                foreach (var admChatId in adminChatIds)
                {
                    try
                    {
                        await botClient.SendMessage(
                            chatId: admChatId,
                            text: adminText,
                            parseMode: ParseMode.Html,
                            replyMarkup: markup,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Telegram Warning] Не вдалося надіслати сповіщення адміну {admChatId}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telegram Error] Помилка надсилання сповіщення підтримки: {ex.Message}");
        }
    }

    private static bool IsPotentialScamOrPhishing(string text)
    {
        var lower = text.ToLowerInvariant();
        string[] scamKeywords = {
            "cvv", "cvc", "термін дії", "баланс картки", "залишок на картці", "пароль від банку",
            "novaposhta-pay", "olx-pay", "ukrposhta-pay", "safe-pay", "card-verify", "oplata-dostavka",
            "перейдіть за посиланням щоб отримати", "підтвердіть отримання коштів", "передоплата 100%",
            "скиньте на картку", "cutt.ly", "bit.ly", "is.gd"
        };

        return scamKeywords.Any(k => lower.Contains(k));
    }

    private static ReplyKeyboardMarkup GetSupportCancelKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("❌ Скасувати звернення"), new KeyboardButton("« Назад у головне меню") }
        })
        {
            ResizeKeyboard = true
        };
    }

    private static ReplyKeyboardMarkup GetUserKeyboard(bool isVerified, bool notificationsEnabled = true)
    {
        if (isVerified)
        {
            var notifLabel = notificationsEnabled ? "🔔 Сповіщення: Увімкнено ✅" : "🔕 Сповіщення: Вимкнено ❌";
            return new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton("📦 Мої замовлення"), new KeyboardButton("🔍 Перевірити замовлення") },
                new[] { new KeyboardButton("🆘 Написати в підтримку"), new KeyboardButton("🛡️ Безпека платформи") },
                new[] { new KeyboardButton(notifLabel), new KeyboardButton("🌐 Відкрити сайт") }
            })
            {
                ResizeKeyboard = true
            };
        }
        else
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new[] { KeyboardButton.WithRequestContact("📱 Надіслати мій номер (Прив'язати акаунт)") },
                new[] { new KeyboardButton("🆘 Написати в підтримку"), new KeyboardButton("🛡️ Безпека платформи") },
                new[] { new KeyboardButton("🔍 Перевірити замовлення"), new KeyboardButton("🌐 Відкрити сайт") }
            })
            {
                ResizeKeyboard = true
            };
        }
    }

    private static InlineKeyboardMarkup GetInlineFaqNavigation()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📦 Як оформити замовлення товару?", "faq_orders") },
            new[] { InlineKeyboardButton.WithCallbackData("🛠 Як замовити послугу майстра?", "faq_services") },
            new[] { InlineKeyboardButton.WithCallbackData("🛡️ Правила безпеки та захист від шахраїв", "faq_safety") },
            new[] { InlineKeyboardButton.WithCallbackData("🔍 Перевірити замовлення за номером", "btn_check_order") },
            new[] { InlineKeyboardButton.WithCallbackData("🆘 Написати в підтримку", "btn_start_support") },
            new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") }
        });
    }

    // ==========================================
    // 🛡️ ЕКСКЛЮЗИВНИЙ АДМІНІСТРАТИВНИЙ КОКПІТ В TELEGRAM
    // ==========================================
    private async Task HandleAdminMessageAsync(
        ITelegramBotClient botClient,
        Telegram.Bot.Types.Message message,
        string rawText,
        string lowerText,
        long chatId,
        AppDbContext db,
        TradeStorm.Api.Models.User? adminUser,
        CancellationToken cancellationToken)
    {
        // 1. Скасування розсилки або повернення в меню
        if (rawText.Equals("❌ Скасувати розсилку", StringComparison.OrdinalIgnoreCase) ||
            rawText.Equals("« Головне меню адміна", StringComparison.OrdinalIgnoreCase) ||
            rawText.Equals("/cancel", StringComparison.OrdinalIgnoreCase) ||
            lowerText == "скасувати" || lowerText == "відміна")
        {
            AdminBroadcastState.TryRemove(chatId, out _);
            AdminReplyingToTicketState.TryRemove(chatId, out _);
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ <b>Дію скасовано.</b> Ви повернулися до панелі керування адміністратора.",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        // 1.5. Офіційна відповідь на тікет підтримки
        if (AdminReplyingToTicketState.TryGetValue(chatId, out int replyTicketId))
        {
            if (rawText.Length < 2)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Будь ласка, введіть змістовний текст відповіді або скасуйте дію.",
                    replyMarkup: GetAdminCancelKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            AdminReplyingToTicketState.TryRemove(chatId, out _);

            var complaint = await db.Complaints
                .Include(c => c.Reporter)
                .Include(c => c.TargetUser)
                .FirstOrDefaultAsync(c => c.Id == replyTicketId, cancellationToken);

            if (complaint == null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Тікет не знайдено або вже видалено.",
                    replyMarkup: GetAdminKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            var targetUser = complaint.Reporter ?? complaint.TargetUser;
            if (targetUser == null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Не вдалося визначити одержувача відповіді.",
                    replyMarkup: GetAdminKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            int adminId = adminUser?.Id ?? 1;

            // 1. Повідомлення у внутрішній чат сайту
            var chatMsg = new TradeStorm.Api.Models.Message
            {
                SenderId = adminId,
                ReceiverId = targetUser.Id,
                Text = $"🛡️ [Служба підтримки Loop Market • {complaint.Reason}]\n\n{rawText.Trim()}\n\nЗ повагою, команда платформи Loop Market",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            db.Messages.Add(chatMsg);

            // 2. Внутрішнє системне сповіщення
            var notif = new Notification
            {
                UserId = targetUser.Id,
                Title = $"💬 Відповідь підтримки на {complaint.Reason}",
                Message = rawText.Trim(),
                Link = "/messages.html",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            db.Notifications.Add(notif);

            // 3. Офіційне сповіщення через бота користувачеві
            if (targetUser.TelegramChatId != 0 && targetUser.ReceiveTelegramNotifications)
            {
                try
                {
                    var userPush = "🛡️ <b>ВІДПОВІДЬ СЛУЖБИ ПІДТРИМКИ LOOP MARKET</b>\n" +
                                   "━━━━━━━━━━━━━━━━━━\n" +
                                   $"По вашому зверненню <i>«{System.Net.WebUtility.HtmlEncode(complaint.Reason)}»</i>:\n\n" +
                                   $"{System.Net.WebUtility.HtmlEncode(rawText.Trim())}\n\n" +
                                   "━━━━━━━━━━━━━━━━━━\n" +
                                   "💬 <i>Ви також можете відповісти в чаті платформи на сайті.</i>";

                    await botClient.SendMessage(
                        chatId: targetUser.TelegramChatId,
                        text: userPush,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                }
                catch { }
            }

            // 4. Оновлення статусу тікета
            complaint.Status = ComplaintStatus.Resolved;
            complaint.ResolutionNotes = $"Офіційна відповідь: {rawText.Trim()}";
            
            // 5. Журнал аудиту дій
            var audit = new AdminAuditLog
            {
                AdminId = adminId,
                AdminEmail = adminUser?.Email ?? "telegram_admin@loopmarket.com",
                Action = "ReplyComplaint",
                TargetType = "Complaint",
                TargetId = complaint.Id,
                Details = $"Відповідь користувачу {targetUser.Email}: {rawText.Trim()}",
                CreatedAt = DateTime.UtcNow
            };
            db.AdminAuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ <b>Офіційну відповідь успішно надіслано!</b>\n\n" +
                      $"🎫 Тікет #{complaint.Id} позначено як <b>Вирішено</b>.\n" +
                      $"👤 Одержувач: <b>{targetUser.FirstName} {targetUser.LastName}</b> ({targetUser.Email})\n" +
                      "💬 Доставлено у внутрішній чат сайту та офіційний Telegram-бот користувача.",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        // 2. Виконання масової розсилки
        if (AdminBroadcastState.TryGetValue(chatId, out bool inBroadcast) && inBroadcast)
        {
            if (rawText.Length < 3)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Будь ласка, введіть змістовне повідомлення для розсилки або натисніть «❌ Скасувати розсилку».",
                    replyMarkup: GetAdminCancelKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            AdminBroadcastState.TryRemove(chatId, out _);
            await botClient.SendMessage(
                chatId: chatId,
                text: "⏳ <i>Розсилку запущено, надсилаємо користувачам...</i>",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            var allUsers = await db.Users.ToListAsync(cancellationToken);
            int inAppCount = 0;
            int tgCount = 0;

            foreach (var u in allUsers)
            {
                var notif = new Notification
                {
                    UserId = u.Id,
                    Title = "📢 Оголошення LoopMarket",
                    Message = rawText,
                    Link = "/index.html",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                db.Notifications.Add(notif);
                inAppCount++;

                if (u.TelegramChatId != 0 && u.ReceiveTelegramNotifications && u.TelegramChatId != chatId)
                {
                    try
                    {
                        var pushText = $"📢 <b>ОГОЛОШЕННЯ ВІД LOOP MARKET</b>\n━━━━━━━━━━━━━━━━━━\n{System.Net.WebUtility.HtmlEncode(rawText)}\n\n<i>З повагою, команда платформи Loop Market</i>";
                        await botClient.SendMessage(
                            chatId: u.TelegramChatId,
                            text: pushText,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                        tgCount++;
                    }
                    catch { }
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ <b>Масову розсилку успішно завершено!</b>\n\n" +
                      $"📨 Внутрішніх сповіщень створено: <b>{inAppCount}</b>\n" +
                      $"📱 Telegram-повідомлень доставлено: <b>{tgCount}</b>",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        // 3. 📊 Статистика платформи
        if (lowerText.Contains("статистик") || lowerText == "/stats")
        {
            var totalUsers = await db.Users.CountAsync(cancellationToken);
            var verifiedUsers = await db.Users.CountAsync(u => u.IsPhoneVerified, cancellationToken);
            var bannedUsers = await db.Users.CountAsync(u => u.IsBanned, cancellationToken);

            var totalListings = await db.Listings.CountAsync(cancellationToken);
            var featuredListings = await db.Listings.CountAsync(l => l.IsFeatured, cancellationToken);
            var servicesCount = await db.Listings.CountAsync(l => l.CategoryId == 8, cancellationToken);
            var productsCount = totalListings - servicesCount;

            var totalOrders = await db.Orders.CountAsync(cancellationToken);
            var pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken);
            var shippedOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Shipped, cancellationToken);
            var completedOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Completed, cancellationToken);
            var totalVolume = await db.Orders.Where(o => o.Status == OrderStatus.Completed).SumAsync(o => (decimal?)o.TotalPrice, cancellationToken) ?? 0m;

            var pendingComplaints = await db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Pending, cancellationToken);
            var resolvedComplaints = await db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Resolved, cancellationToken);

            var statsMsg = "📊 <b>ЗВОДКА ТА СТАТИСТИКА LOOP MARKET:</b>\n" +
                           "━━━━━━━━━━━━━━━━━━\n" +
                           "👥 <b>Користувачі:</b>\n" +
                           $"• Зареєстровано: <b>{totalUsers}</b>\n" +
                           $"• Верифіковані: <b>{verifiedUsers}</b>\n" +
                           $"• Заблоковано: <b>{bannedUsers}</b>\n\n" +
                           "📦 <b>Оголошення:</b>\n" +
                           $"• Всього на сайті: <b>{totalListings}</b> (Товари: {productsCount}, Послуги: {servicesCount})\n" +
                           $"• Закріплені ТОП: <b>{featuredListings}</b>\n\n" +
                           "🚚 <b>Угоди та Замовлення:</b>\n" +
                           $"• Всього замовлень: <b>{totalOrders}</b>\n" +
                           $"• Очікують підтвердження: <b>{pendingOrders}</b>\n" +
                           $"• Відправлено (в дорозі): <b>{shippedOrders}</b>\n" +
                           $"• Успішно завершено: <b>{completedOrders}</b>\n" +
                           $"• Оборот завершених угод: <b>{totalVolume:N0} ₴</b>\n\n" +
                           "🚨 <b>Безпека та Тікети:</b>\n" +
                           $"• Очікують розгляду: <b>{pendingComplaints}</b>\n" +
                           $"• Вирішено модерацією: <b>{resolvedComplaints}</b>\n" +
                           "━━━━━━━━━━━━━━━━━━";

            await botClient.SendMessage(
                chatId: chatId,
                text: statsMsg,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("🚨 Переглянути активні тікети", "adm_tickets") },
                    new[] { InlineKeyboardButton.WithUrl("🌐 Повна веб-панель", "https://loopmarket-x0uo.onrender.com/admin.html") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 4. 🚨 Активні звернення та тікети
        if (lowerText.Contains("тікет") || lowerText.Contains("скарг") || lowerText == "/tickets")
        {
            var pending = await db.Complaints
                .Include(c => c.Reporter)
                .Include(c => c.TargetUser)
                .Where(c => c.Status == ComplaintStatus.Pending)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "✅ <b>Всі тікети та скарги опрацьовано!</b>\n\nНових нерозглянутих звернень немає. Платформа працює в штатному режимі.",
                    parseMode: ParseMode.Html,
                    replyMarkup: GetAdminKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: $"🚨 <b>Знайдено {pending.Count} очікуючих звернень:</b>\n━━━━━━━━━━━━━━━━━━",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            foreach (var c in pending)
            {
                string? tgUser = null;
                if (!string.IsNullOrWhiteSpace(c.Details))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(c.Details, @"@([a-zA-Z0-9_]{3,32})");
                    if (m.Success) tgUser = m.Groups[1].Value;
                }

                var repName = c.Reporter != null ? $"{c.Reporter.FirstName} {c.Reporter.LastName}".Trim() : "Користувач";
                var repPhone = c.Reporter?.PhoneNumber ?? "Не вказано";

                var card = $"🎫 <b>Тікет #{c.Id} • {System.Net.WebUtility.HtmlEncode(c.Reason)}</b>\n" +
                           $"👤 Заявник: <b>{System.Net.WebUtility.HtmlEncode(repName)}</b> (тел: <code>{repPhone}</code>)\n" +
                           $"🕒 Час: {c.CreatedAt:dd.MM.yyyy HH:mm}\n" +
                           (!string.IsNullOrWhiteSpace(c.Details) ? $"\n📝 <i>{System.Net.WebUtility.HtmlEncode(c.Details)}</i>\n" : "");

                var buttons = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("💬 Відповісти в системі", $"adm_rep_{c.Id}"),
                    InlineKeyboardButton.WithCallbackData("✅ Вирішити", $"adm_res_{c.Id}")
                };
                buttons.Add(InlineKeyboardButton.WithCallbackData("❌ Відхилити", $"adm_dsm_{c.Id}"));
                if (c.TargetUserId > 0)
                {
                    buttons.Add(InlineKeyboardButton.WithCallbackData("🚫 Бан", $"adm_ban_{c.TargetUserId}"));
                }

                var markup = new InlineKeyboardMarkup(new[]
                {
                    buttons.Take(2).ToArray(),
                    buttons.Skip(2).ToArray()
                });

                await botClient.SendMessage(
                    chatId: chatId,
                    text: card,
                    parseMode: ParseMode.Html,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken);
            }
            return;
        }

        // 5. 🚚 Останні замовлення платформи
        if (lowerText.Contains("замовленн") || lowerText.Contains("угод") || lowerText == "/orders")
        {
            var orders = await db.Orders
                .Include(o => o.Listing)
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (orders.Count == 0)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "📦 На платформі поки немає створених замовлень.",
                    replyMarkup: GetAdminKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            var msg = "🚚 <b>ОСТАННІ ЗАМОВЛЕННЯ ПЛАТФОРМИ LOOP MARKET:</b>\n━━━━━━━━━━━━━━━━━━\n\n";
            foreach (var o in orders)
            {
                var buyerName = o.Buyer != null ? $"{o.Buyer.FirstName} {o.Buyer.LastName}".Trim() : "Покупець";
                var sellerName = o.Seller != null ? $"{o.Seller.FirstName} {o.Seller.LastName}".Trim() : "Продавець";
                var statusUk = o.Status switch
                {
                    OrderStatus.Pending => "⏳ Очікує",
                    OrderStatus.Confirmed => "✅ Підтверджено",
                    OrderStatus.Shipped => "🚚 Відправлено",
                    OrderStatus.Completed => "🎉 Завершено",
                    OrderStatus.Cancelled => "❌ Скасовано",
                    _ => o.Status
                };

                msg += $"• <b>{o.GetDisplayCode()}</b> ({statusUk})\n" +
                       $"  🏷 {System.Net.WebUtility.HtmlEncode(o.Listing?.Title ?? "Оголошення")} — <b>{o.TotalPrice:N0} ₴</b>\n" +
                       $"  🛒 {buyerName} (<code>{o.Buyer?.PhoneNumber}</code>) ➡️ 🏷 {sellerName}\n" +
                       $"  📍 {o.DeliveryCity}, {o.DeliveryBranch}\n" +
                       (!string.IsNullOrWhiteSpace(o.TrackingNumber) ? $"  ТТН: <code>{o.TrackingNumber}</code>\n" : "") +
                       $"  🕒 {o.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
            }
            msg += "━━━━━━━━━━━━━━━━━━";

            await botClient.SendMessage(
                chatId: chatId,
                text: msg,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("🌐 Арбітраж угод в адмінці", "https://loopmarket-x0uo.onrender.com/admin.html#orders") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 6. 👥 Останні зареєстровані користувачі
        if (lowerText.Contains("користувач") || lowerText == "/users")
        {
            var users = await db.Users
                .OrderByDescending(u => u.Id)
                .Take(5)
                .ToListAsync(cancellationToken);

            var msg = "👥 <b>ОСТАННІ ЗАРЕЄСТРОВАНІ КОРИСТУВАЧІ:</b>\n━━━━━━━━━━━━━━━━━━\n\n";
            foreach (var u in users)
            {
                var status = u.IsBanned ? "🚫 ЗАБЛОКОВАНО" : "🟢 Активний";
                var verified = u.IsPhoneVerified ? "✅ Підтверджено" : "❌ Не підтверджено";
                msg += $"• <b>#{u.Id}: {System.Net.WebUtility.HtmlEncode(u.FirstName)} {System.Net.WebUtility.HtmlEncode(u.LastName)}</b>\n" +
                       $"  📧 {System.Net.WebUtility.HtmlEncode(u.Email)} | 📱 <code>{u.PhoneNumber ?? "немає"}</code>\n" +
                       $"  Роль: <b>{u.Role}</b> | Верифікація: {verified}\n" +
                       $"  Статус: <b>{status}</b>\n\n";
            }
            msg += "━━━━━━━━━━━━━━━━━━";

            await botClient.SendMessage(
                chatId: chatId,
                text: msg,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("🌐 Керування в адмінці", "https://loopmarket-x0uo.onrender.com/admin.html#users") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 7. 📢 Режим розсилки
        if (lowerText.Contains("розсилк") || lowerText == "/broadcast")
        {
            AdminBroadcastState[chatId] = true;
            await botClient.SendMessage(
                chatId: chatId,
                text: "📢 <b>РЕЖИМ МАСОВОЇ СИСТЕМНОЇ РОЗСИЛКИ</b>\n" +
                      "━━━━━━━━━━━━━━━━━━\n" +
                      "Надішліть текст оголошення наступним повідомленням у цей чат.\n\n" +
                      "Воно буде доставлено усім зареєстрованим користувачам платформи (в особистий кабінет на сайті та в Telegram).\n\n" +
                      "<i>Для скасування натисніть «❌ Скасувати розсилку» нижче:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminCancelKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        // 8. 🌐 Відкрити веб-адмінку
        if (lowerText.Contains("адмін") || lowerText.Contains("панель") || lowerText == "/admin")
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🌐 <b>Офіційна панель оператора безпеки Loop Market:</b>\n\n" +
                      "https://loopmarket-x0uo.onrender.com/admin.html\n\n" +
                      "<i>Панель оптимізована для мобільних телефонів та комп'ютерів.</i>",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("🚀 Відкрити панель на сайті", "https://loopmarket-x0uo.onrender.com/admin.html") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 9. Перевірка замовлення адміністратором (Повні незамасковані дані)
        var orderCode = ExtractOrderCodeCandidate(rawText);
        if (orderCode != null)
        {
            var foundOrder = await db.Orders
                .Include(o => o.Listing)
                .Include(o => o.Seller)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode || o.Id.ToString() == orderCode, cancellationToken);

            if (foundOrder != null)
            {
                var buyerName = foundOrder.Buyer != null ? $"{foundOrder.Buyer.FirstName} {foundOrder.Buyer.LastName}".Trim() : "Покупець";
                var sellerName = foundOrder.Seller != null ? $"{foundOrder.Seller.FirstName} {foundOrder.Seller.LastName}".Trim() : "Продавець";

                var adminOrderMsg = $"📦 <b>ДЕТАЛІ ЗАМОВЛЕННЯ {foundOrder.GetDisplayCode()} (АДМІН-РЕЖИМ):</b>\n" +
                                    $"━━━━━━━━━━━━━━━━━━\n" +
                                    $"🏷 <b>Оголошення:</b> {System.Net.WebUtility.HtmlEncode(foundOrder.Listing?.Title ?? "Товар")}\n" +
                                    $"💰 <b>Сума:</b> {foundOrder.TotalPrice:N0} ₴ | Оплата: {foundOrder.PaymentMethod}\n" +
                                    $"⚡ <b>Статус:</b> <b>{foundOrder.Status}</b>\n\n" +
                                    $"🛒 <b>Покупець:</b> {buyerName}\n" +
                                    $"   Телефон: <code>{foundOrder.Buyer?.PhoneNumber ?? "немає"}</code> | Email: {foundOrder.Buyer?.Email}\n\n" +
                                    $"🏷 <b>Продавець:</b> {sellerName}\n" +
                                    $"   Телефон: <code>{foundOrder.Seller?.PhoneNumber ?? "немає"}</code> | Email: {foundOrder.Seller?.Email}\n\n" +
                                    $"📍 <b>Доставка:</b> {foundOrder.DeliveryCity}, {foundOrder.DeliveryBranch}\n" +
                                    (!string.IsNullOrWhiteSpace(foundOrder.TrackingNumber) ? $"🚚 <b>ТТН:</b> <code>{foundOrder.TrackingNumber}</code>\n" : "") +
                                    $"━━━━━━━━━━━━━━━━━━";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: adminOrderMsg,
                    parseMode: ParseMode.Html,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithUrl("🌐 Відкрити в адмінці", "https://loopmarket-x0uo.onrender.com/admin.html#orders") }
                    }),
                    cancellationToken: cancellationToken);
                return;
            }
        }

        // 10. Дефолтне адмін-меню
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🛡️ <b>ЦЕНТР КЕРУВАННЯ АДМІНІСТРАТОРА LOOP MARKET</b>\n" +
                      "━━━━━━━━━━━━━━━━━━\n" +
                      "Вітаємо, <b>@LoopMarketSupport</b>!\n" +
                      "Ви увійшли в режим оперативного керування та безпеки платформи.\n\n" +
                      "⚡ <b>Доступні швидкі функції:</b>\n" +
                      "• 📊 <b>Статистика:</b> огляд користувачів, замовлень та обороту\n" +
                      "• 🚨 <b>Тікети та скарги:</b> моніторинг звернень та вирішення\n" +
                      "• 🚚 <b>Замовлення:</b> останні угоди по всій платформі\n" +
                      "• 👥 <b>Користувачі:</b> нові реєстрації та модерація\n" +
                      "• 📢 <b>Розсилка:</b> надсилання масових сповіщень\n" +
                      "• 🌐 <b>Веб-панель:</b> повна панель оператора безпеки\n\n" +
                      "<i>Оберіть потрібну дію на панелі нижче:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: GetAdminKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    private static ReplyKeyboardMarkup GetAdminKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📊 Статистика платформи"), new KeyboardButton("🚨 Активні звернення та тікети") },
            new[] { new KeyboardButton("🚚 Останні замовлення"), new KeyboardButton("👥 Останні користувачі") },
            new[] { new KeyboardButton("📢 Надіслати розсилку"), new KeyboardButton("🌐 Відкрити веб-адмінку") }
        })
        {
            ResizeKeyboard = true
        };
    }

    private static ReplyKeyboardMarkup GetAdminCancelKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("❌ Скасувати розсилку"), new KeyboardButton("« Головне меню адміна") }
        })
        {
            ResizeKeyboard = true
        };
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Помилка Telegram Bot API: {exception.Message}");
        return Task.CompletedTask;
    }
}
