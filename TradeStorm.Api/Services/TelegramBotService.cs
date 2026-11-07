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

            case "faq_check_order":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🔍 <b>Перевірка статусу замовлення:</b>\n\n" +
                          "Ви можете надіслати просто 6 цифр номера замовлення, наприклад:\n" +
                          "👉 <code>782194</code>\n" +
                          "👉 або повний код <code>LP-782194</code>\n" +
                          "👉 або старий номер <code>#14</code>\n\n" +
                          "Я миттєво знайду інформацію в системі та покажу актуальний статус посилки й номер ТТН!",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                break;

            case "support_contact":
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🆘 <b>Служба турботи та підтримки користувачів Loop Market:</b>\n\n" +
                          "• 👨‍💻 Головний черговий адміністратор: <b>@LoopMarketSupport</b>\n" +
                          "• ✉️ Офіційна пошта: <code>support@loopmarket.ua</code>\n" +
                          "• 🕒 Графік роботи: щодня з 08:00 до 22:00\n\n" +
                          "💬 <b>Потрібна допомога прямо зараз?</b>\n" +
                          "Натисніть кнопку нижче, щоб написати адміністратору в Telegram, або просто опишіть питання в цей чат — ми зареєструємо звернення!",
                    parseMode: ParseMode.Html,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") },
                        new[] { InlineKeyboardButton.WithUrl("🌐 Центр допомоги на сайті", "https://loopmarket-x0uo.onrender.com/about.html") }
                    }),
                    cancellationToken: cancellationToken);
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
            if (MasterSupportChatId != chatId)
            {
                MasterSupportChatId = chatId;
                Console.WriteLine($"[Telegram Admin] Головний чат підтримки підключено: @LoopMarketSupport (ChatId: {chatId})");
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🛡️ <b>Вітаємо, Адміністраторе!</b>\n\n" +
                          "Ваш акаунт <b>@LoopMarketSupport</b> успішно активовано як головну лінію підтримки платформи Loop Market.\n" +
                          "Усі звернення користувачів, запити на допомогу та скарги на шахраїв надходитимуть сюди в реальному часі.",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
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

        // 3. 🚨 АНТИ-ШАХРАЙ АНАЛІЗАТОР ТА БЕЗПЕКА (SCAM & PHISHING DETECTION)
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

        // 4. 🔍 ГНУЧКА ТА БЕЗПЕЧНА ПЕРЕВІРКА ЗАМОВЛЕННЯ
        // Користувач може ввести: 6 цифр (782194), або LP-782194, або lp 782194, або #14
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
                        .OrderByDescending(o => o.Id)
                        .Take(150)
                        .ToListAsync(cancellationToken);
                    foundOrder = recentOrders.FirstOrDefault(o => o.GetDisplayCode().Equals(extractedOrderCode, StringComparison.OrdinalIgnoreCase));
                }

                // Також перевіряємо, чи цифри не відповідають старій ID замовлення (якщо ввели #15 чи 15)
                if (foundOrder == null && int.TryParse(extractedOrderCode.Substring(3), out int rawId) && rawId < 1000)
                {
                    foundOrder = await db.Orders
                        .Include(o => o.Listing)
                        .Include(o => o.Seller)
                        .Include(o => o.Buyer)
                        .FirstOrDefaultAsync(o => o.Id == rawId, cancellationToken);
                }
            }
            else if (extractedOrderCode.StartsWith("#") && int.TryParse(extractedOrderCode.TrimStart('#'), out int parsedId))
            {
                foundOrder = await db.Orders
                    .Include(o => o.Listing)
                    .Include(o => o.Seller)
                    .Include(o => o.Buyer)
                    .FirstOrDefaultAsync(o => o.Id == parsedId, cancellationToken);
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

        // 7. 🆘 КНОПКА "ДОПОМОГА / FAQ / ПІДТРИМКА"
        if (lowerText.Contains("допомог") || lowerText.Contains("підтримк") || lowerText.Contains("help") || lowerText.Contains("faq") || lowerText == "/help" || lowerText.Contains("оператор") || lowerText.Contains("sos"))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "👋 <b>Служба швидкої підтримки та безпеки Loop Market</b>\n\n" +
                      "Я допоможу швидко вирішити будь-яке питання або зв'язати вас з черговим оператором <b>@LoopMarketSupport</b>.\n\n" +
                      "Оберіть тему вашого питання або просто напишіть повідомлення в цей чат:",
                parseMode: ParseMode.Html,
                replyMarkup: GetInlineFaqNavigation(),
                cancellationToken: cancellationToken);
            return;
        }

        // 8. 🔍 КНОПКА "ПЕРЕВІРИТИ ЗАМОВЛЕННЯ"
        if (lowerText.Contains("перевірити замовлення") || lowerText == "/track")
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🔍 <b>Перевірка статусу замовлення в 1 клік:</b>\n\n" +
                      "Надішліть сюди у чат номер або код вашого замовлення, наприклад:\n" +
                      "👉 просто цифри: <code>749281</code>\n" +
                      "👉 або з префіксом: <code>LP-749281</code>\n" +
                      "👉 або <code>#14</code>\n\n" +
                      "Я миттєво покажу статус відправлення, ТТН та інформацію про посилку!",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        // 9. 🔔 КНОПКА ПЕРЕМИКАННЯ СПОВІЩЕНЬ
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

        // 10. 🌐 ВІДКРИТИ САЙТ
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

        // 11. КОРИСТУВАЧ ПИШЕ ТЕКСТОВЕ ЗВЕРНЕННЯ / ПИТАННЯ В ПІДТРИМКУ
        if (rawText.Length > 3 && !rawText.StartsWith("/"))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            var userName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : message.Chat.FirstName ?? "Користувач Telegram";
            var userPhone = user?.PhoneNumber ?? "Не прив'язано";
            var ticketCode = $"TK-{Random.Shared.Next(100000, 999999)}";

            // Пересилаємо звернення адміністратору @LoopMarketSupport в Telegram та в базу даних
            await NotifyAdminsAboutSupportTicketAsync(
                botClient,
                chatId,
                userName,
                userPhone,
                message.Chat.Username,
                ticketCode,
                rawText,
                cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ <b>Ваше звернення зареєстровано в системі підтримки!</b>\n\n" +
                      $"🎫 Код звернення: <code>{ticketCode}</code>\n" +
                      $"👤 Відправник: <b>{System.Net.WebUtility.HtmlEncode(userName)}</b>\n\n" +
                      $"📩 Повідомлення вже передано черговому адміністратору <b>@LoopMarketSupport</b>.\n" +
                      $"Вам дадуть відповідь у найкоротший термін!\n\n" +
                      $"<i>Ви також можете написати черговому оператору напряму за кнопкою нижче:</i>",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") }
                }),
                cancellationToken: cancellationToken);
            return;
        }

        // 12. ДЕФОЛТНЕ ПРИВІТАННЯ ТА ГОЛОВНЕ МЕНЮ
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, cancellationToken);
            var isVerified = user != null && user.IsPhoneVerified;

            var greeting = isVerified 
                ? $"👋 <b>Раді вітати, {user!.FirstName}!</b>\n\nЯ ваш офіційний бот-асистент <b>Loop Market</b>. Оберіть потрібну дію на клавіатурі нижче:"
                : "👋 <b>Вітаємо в офіційному боті Loop Market!</b>\n\n" +
                  "Тут ви можете миттєво перевіряти статус замовлень (просто введіть цифри замовлення), отримувати сповіщення про посилки та звертатися до служби безпеки й підтримки.\n\n" +
                  "Для початку підтвердіть ваш номер телефону натисканням кнопки нижче («📱 Надіслати мій номер»).";

            await botClient.SendMessage(
                chatId: chatId,
                text: greeting,
                parseMode: ParseMode.Html,
                replyMarkup: GetUserKeyboard(isVerified, user?.ReceiveTelegramNotifications ?? true),
                cancellationToken: cancellationToken);
        }
    }

    private static string? ExtractOrderCodeCandidate(string text)
    {
        var raw = text.Trim();

        // 1. Повний префікс LP-XXXXXX, lp XXXXXX або LPXXXXXX
        var lpMatch = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?:LP[- ]?([0-9A-Za-z_-]{4,10}))\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (lpMatch.Success)
        {
            return $"LP-{lpMatch.Groups[1].Value.ToUpperInvariant()}";
        }

        // 2. Введення просто 5-7 цифр (наприклад, 749281 чи 123456)
        var digitsMatch = System.Text.RegularExpressions.Regex.Match(raw, @"^\s*(\d{5,7})\s*$");
        if (digitsMatch.Success)
        {
            return $"LP-{digitsMatch.Groups[1].Value}";
        }

        // 3. Старий порядковий формат #15 чи 1-4 цифри з решіткою
        var hashMatch = System.Text.RegularExpressions.Regex.Match(raw, @"#(\d+)");
        if (hashMatch.Success)
        {
            return hashMatch.Value;
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

            // 1. Зберігаємо сповіщення в веб-кабінеті адміністраторів
            var adminUsers = await db.Users.Where(u => u.Role == UserRoles.Admin).ToListAsync(cancellationToken);
            foreach (var adm in adminUsers)
            {
                var notif = new Notification
                {
                    UserId = adm.Id,
                    Title = $"🆘 Звернення {ticketCode} в Telegram",
                    Message = $"{userName} (тел. {userPhone}, ChatId: {userChatId}): {userMessageText}",
                    Link = "/admin.html",
                    CreatedAt = DateTime.UtcNow
                };
                db.Notifications.Add(notif);
            }
            await db.SaveChangesAsync(cancellationToken);

            // 2. Відправляємо миттєвий Telegram push головному адміністратору @LoopMarketSupport та підключеним адмінам
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

    private static ReplyKeyboardMarkup GetUserKeyboard(bool isVerified, bool notificationsEnabled = true)
    {
        if (isVerified)
        {
            var notifLabel = notificationsEnabled ? "🔔 Сповіщення: Увімкнено ✅" : "🔕 Сповіщення: Вимкнено ❌";
            return new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton("📦 Мої замовлення"), new KeyboardButton("🔍 Перевірити замовлення") },
                new[] { new KeyboardButton("🛡️ Безпека платформи"), new KeyboardButton("🆘 Допомога / Підтримка") },
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
                new[] { new KeyboardButton("🛡️ Безпека платформи"), new KeyboardButton("🆘 Допомога / Підтримка") },
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
            new[] { InlineKeyboardButton.WithCallbackData("🔍 Перевірити замовлення за номером", "faq_check_order") },
            new[] { InlineKeyboardButton.WithUrl("💬 Написати адміну: @LoopMarketSupport", "https://t.me/LoopMarketSupport") }
        });
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Помилка Telegram Bot API: {exception.Message}");
        return Task.CompletedTask;
    }
}
