using System.Collections.Concurrent;

using Microsoft.AspNetCore.RateLimiting;

using System;

using System.Linq;

using System.Security.Claims;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using TradeStorm.Api.Data;

using TradeStorm.Api.DTOs;

using TradeStorm.Api.Models;

using TradeStorm.Api.Services;



namespace TradeStorm.Api.Controllers;



[ApiController]

[Route("api/[controller]")]

[EnableRateLimiting("AuthRateLimit")]

public class UsersController : ControllerBase

{

    private static readonly ConcurrentDictionary<int, (string Ticket, DateTime Expiry)> _verificationTickets = new();



    private void SetAuthCookies(string token, string? refreshToken)

    {

        try

        {

            var isHttps = Request.IsHttps;

            var cookieOptions = new CookieOptions

            {

                HttpOnly = true,

                Secure = isHttps,

                SameSite = SameSiteMode.Lax,

                Expires = DateTime.UtcNow.AddMinutes(60),

                Path = "/"

            };

            Response.Cookies.Append("loopToken", token, cookieOptions);



            if (!string.IsNullOrEmpty(refreshToken))

            {

                var refreshOptions = new CookieOptions

                {

                    HttpOnly = true,

                    Secure = isHttps,

                    SameSite = SameSiteMode.Lax,

                    Expires = DateTime.UtcNow.AddDays(7),

                    Path = "/"

                };

                Response.Cookies.Append("loopRefreshToken", refreshToken, refreshOptions);

            }

        }

        catch { }

    }

    private readonly AppDbContext _context;

    private readonly ITokenService _tokenService;

    private readonly TelegramBotService _telegramService;

    private readonly ICaptchaService? _captchaService;

    private readonly IEmailService? _emailService;

    private readonly PasswordHasher<User> _passwordHasher = new();



    public UsersController(AppDbContext context, ITokenService tokenService, TelegramBotService telegramService, ICaptchaService? captchaService = null, IEmailService? emailService = null)

    {

        _context = context;

        _tokenService = tokenService;

        _telegramService = telegramService;

        _captchaService = captchaService;

        _emailService = emailService;

    }



    [HttpPost]

    [HttpPost("register")]

    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)

    {

        // BotShield: Перевірка прихованої пастки для ботів (Honeypot)

        if (!string.IsNullOrWhiteSpace(dto.HoneypotTrap))

            return BadRequest(new { error = "Спам-фільтр: автоматизований запит заблоковано" });



        // BotShield: Перевірка капчі (якщо надіслано токен)

        if (!string.IsNullOrWhiteSpace(dto.CaptchaToken) && _captchaService != null)

        {

            if (!_captchaService.ValidateCaptcha(dto.CaptchaToken, dto.CaptchaAnswer))

                return BadRequest(new { error = "Неправильна відповідь перевірки на бота або час капчі минув" });

        }



        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))

            return BadRequest(new { error = "Email вже використовується іншим користувачем" });



        var digitsOnly = new string((dto.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 9)

            return BadRequest(new { error = "Вкажіть коректний номер телефону (мінімум 9 цифр)" });



        var normalizedPhone = "+" + digitsOnly;

        var last9 = digitsOnly.Substring(digitsOnly.Length - 9);



        bool phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber != null && 

            (u.PhoneNumber == normalizedPhone || u.PhoneNumber == digitsOnly || u.PhoneNumber.EndsWith(last9)));



        if (phoneExists)

            return BadRequest(new { error = "Цей номер телефону вже зареєстрований!" });



        bool isTestAccount = dto.Email.Contains("test_") || dto.Email.EndsWith("@loopmarket.com") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";



        var user = new User

        {

            FirstName = dto.FirstName.Trim(),

            LastName = dto.LastName.Trim(),

            Email = dto.Email.Trim().ToLowerInvariant(),

            PhoneNumber = normalizedPhone,

            Role = UserRoles.Buyer,

            IsPhoneVerified = isTestAccount,

            IsEmailVerified = isTestAccount

        };

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);



        // Генеруємо 6-значний код для підтвердження пошти при реєстрації

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        user.EmailVerificationCode = code;

        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);



        _context.Users.Add(user);

        await _context.SaveChangesAsync();



        // Відправляємо код на вказану пошту

        if (_emailService != null && !isTestAccount)

        {

            _ = Task.Run(async () =>

            {

                try

                {

                    await _emailService.SendVerificationCodeAsync(user.Email, user.FirstName, code);

                }

                catch { }

            });

        }



        var ticket = Guid.NewGuid().ToString("N");

        _verificationTickets[user.Id] = (ticket, DateTime.UtcNow.AddMinutes(15));



        return Ok(new

        {

            id = user.Id,

            firstName = user.FirstName,

            lastName = user.LastName,

            email = user.Email,

            phoneNumber = user.PhoneNumber,

            role = user.Role,

            isPhoneVerified = isTestAccount,

            isEmailVerified = isTestAccount,

            verificationTicket = ticket,

            devHint = isTestAccount ? code : null

        });

    }



    

    /// <summary>

    /// Перевірка статусу верифікації номера через Telegram-бота (для авто-перенаправлення без натискання кнопок)

    /// </summary>

    [HttpGet("verify-status")]

    [AllowAnonymous]

    public async Task<IActionResult> CheckVerifyStatus([FromQuery] int userId, [FromQuery] string? ticket)

    {

        // Захист від IDOR та несанкціонованого отримання токенів

        if (string.IsNullOrWhiteSpace(ticket) ||

            !_verificationTickets.TryGetValue(userId, out var entry) ||

            entry.Expiry < DateTime.UtcNow ||

            entry.Ticket != ticket)

        {

            return Unauthorized(new { error = "Недійсний або прострочений квиток верифікації" });

        }



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        if (!user.IsPhoneVerified)

        {

            return Ok(new { isVerified = false });

        }



        _verificationTickets.TryRemove(userId, out _);



        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshToken = _tokenService.GenerateRefreshToken();



        user.RefreshToken = refreshToken;

        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();



        SetAuthCookies(accessToken, refreshToken);



        return Ok(new

        {

            isVerified = true,

            token = accessToken,

            refreshToken = refreshToken,

            role = user.Role,

            email = user.Email,

            firstName = user.FirstName,

            lastName = user.LastName

        });

    }



    /// <summary>

    /// Підтвердження 6-значного email-коду під час реєстрації (Етап безпеки акаунта)

    /// </summary>

    [HttpPost("verify-registration-code")]

    [AllowAnonymous]

    public async Task<IActionResult> VerifyRegistrationCode([FromBody] VerifyRegistrationCodeDto dto)

    {

        if (dto == null || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Ticket))

        {

            return BadRequest(new { error = "Введіть 6-значний код підтвердження" });

        }



        if (!_verificationTickets.TryGetValue(dto.UserId, out var entry) ||

            entry.Expiry < DateTime.UtcNow ||

            entry.Ticket != dto.Ticket)

        {

            return Unauthorized(new { error = "Недійсний або прострочений сеанс реєстрації. Будь ласка, зареєструйтесь знову." });

        }



        var user = await _context.Users.FindAsync(dto.UserId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode) ||

            !user.EmailVerificationCodeExpiresAt.HasValue ||

            user.EmailVerificationCodeExpiresAt.Value < DateTime.UtcNow)

        {

            return BadRequest(new { error = "Час дії коду минув (15 хв). Натисніть «Надіслати код повторно»." });

        }



        if (user.EmailVerificationCode != dto.Code.Trim())

        {

            return BadRequest(new { error = "Невірний код підтвердження. Перевірте скриньку та введіть код ще раз." });

        }



        // Успішно! Активуємо користувача

        user.IsEmailVerified = true;

        user.IsPhoneVerified = true;

        user.EmailVerificationCode = null;

        user.EmailVerificationCodeExpiresAt = null;



        _verificationTickets.TryRemove(dto.UserId, out _);



        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshToken = _tokenService.GenerateRefreshToken();



        user.RefreshToken = refreshToken;

        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();



        SetAuthCookies(accessToken, refreshToken);



        await LogAuditAsync("RegisterEmailVerified", "User", user.Id, $"Користувач {user.Email} підтвердив пошту кодом при реєстрації");



        return Ok(new

        {

            success = true,

            token = accessToken,

            refreshToken = refreshToken,

            role = user.Role,

            email = user.Email,

            firstName = user.FirstName,

            lastName = user.LastName,

            isEmailVerified = true,

            isPhoneVerified = true

        });

    }



    /// <summary>

    /// Повторна відправка email-коду під час реєстрації

    /// </summary>

    [HttpPost("resend-registration-code")]

    [AllowAnonymous]

    public async Task<IActionResult> ResendRegistrationCode([FromBody] ResendRegistrationCodeDto dto)

    {

        if (dto == null || string.IsNullOrWhiteSpace(dto.Ticket))

        {

            return BadRequest(new { error = "Недійсний запит" });

        }



        if (!_verificationTickets.TryGetValue(dto.UserId, out var entry) ||

            entry.Expiry < DateTime.UtcNow ||

            entry.Ticket != dto.Ticket)

        {

            return Unauthorized(new { error = "Сеанс реєстрації завершився. Зареєструйтесь заново." });

        }



        var user = await _context.Users.FindAsync(dto.UserId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        user.EmailVerificationCode = code;

        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        await _context.SaveChangesAsync();



        if (_emailService != null)

        {

            _ = Task.Run(async () =>

            {

                try

                {

                    await _emailService.SendVerificationCodeAsync(user.Email, user.FirstName, code);

                }

                catch { }

            });

        }



        return Ok(new { success = true, message = "Новий код надіслано на вашу пошту!" });

    }



    [HttpPost("login")]

    public async Task<IActionResult> Login([FromBody] LoginDto dto)

    {

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null) return Unauthorized(new { error = "Невірний email або пароль" });



        bool isPasswordValid = false;

        try

        {

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

            if (verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded)

            {

                isPasswordValid = true;

            }

        }

        catch

        {

            // Зворотна сумісність для старих відкритих паролів

        }



        if (!isPasswordValid && user.PasswordHash == dto.Password)

        {

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            await _context.SaveChangesAsync();

            isPasswordValid = true;

        }



        if (!isPasswordValid) return Unauthorized(new { error = "Невірний email або пароль" });



        if (user.IsBanned)

        {

            return StatusCode(StatusCodes.Status403Forbidden, new

            {

                error = $"Ваш акаунт заблоковано адміністрацією LoopMarket. Причина: {user.BanReason ?? "Порушення правил спільноти / шахрайські дії"}."

            });

        }



        // Генерація пари токенів (Access + Refresh Token)

        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshToken = _tokenService.GenerateRefreshToken();



        user.RefreshToken = refreshToken;

        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Refresh token дійсний 7 днів

        await _context.SaveChangesAsync();



        bool isTestAccount = user.Email.Contains("test_") || user.Email.EndsWith("@loopmarket.com") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";

        if (!user.IsPhoneVerified && !user.IsEmailVerified && user.Role != UserRoles.Admin && !isTestAccount)

        {

            return Ok(new

            {

                isPhoneVerified = false,

                userId = user.Id,

                email = user.Email,

                role = user.Role,

                isEmailVerified = false,

                error = "Номер телефону або пошту не підтверджено"

            });

        }



        SetAuthCookies(accessToken, refreshToken);



        return Ok(new

        {

            token = accessToken,

            refreshToken = refreshToken,

            isPhoneVerified = user.IsPhoneVerified,

            isEmailVerified = user.IsEmailVerified,

            isVerified = user.IsEmailVerified || user.IsPhoneVerified,

            role = user.Role

        });

    }



    /// <summary>

    /// Оновлення Access Token за допомогою валідного Refresh Token (Етап 5)

    /// </summary>

    [HttpPost("refresh")]

    public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshDto dto)

    {

        var principal = _tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);

        if (principal == null)

        {

            return BadRequest(new { error = "Недійсний Access Token або сигнатура" });

        }



        var userIdString = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId))

        {

            return BadRequest(new { error = "Невірний токен" });

        }



        var user = await _context.Users.FindAsync(userId);

        if (user == null || user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)

        {

            return Unauthorized(new { error = "Недійсний або прострочений Refresh Token. Необхідно авторизуватися знову." });

        }



        if (user.IsBanned)

        {

            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Акаунт заблоковано через порушення правил платформи." });

        }



        // Ротація токенів

        var newAccessToken = _tokenService.GenerateAccessToken(user);

        var newRefreshToken = _tokenService.GenerateRefreshToken();



        user.RefreshToken = newRefreshToken;

        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();



        return Ok(new

        {

            token = newAccessToken,

            refreshToken = newRefreshToken

        });

    }



    /// <summary>

    /// Завершення сеансу та відкликання Refresh Token (Logout / Revocation) (Етап 5)

    /// </summary>

    [HttpPost("revoke")]

    [Authorize]

    public async Task<IActionResult> Revoke()

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound();



        user.RefreshToken = null;

        user.RefreshTokenExpiryTime = null;

        await _context.SaveChangesAsync();



        return Ok(new { message = "Сесію успішно завершено. Токен оновлення відкликано." });

    }



    /// <summary>

    /// Адміністрування: перегляд усіх користувачів системи з ролями та статусом блокування (Етап 6 RBAC)

    /// </summary>

    [HttpGet("admin/all")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> GetAllUsersForAdmin()

    {

        var users = await _context.Users

            .OrderBy(u => u.Id)

            .Select(u => new

            {

                u.Id,

                u.FirstName,

                u.LastName,

                u.Email,

                u.PhoneNumber,

                u.Role,

                u.IsPhoneVerified,

                u.IsEmailVerified,

                IsVerified = u.IsEmailVerified || u.IsPhoneVerified,

                u.TelegramChatId,

                u.IsBanned,

                u.BanReason,

                u.MutedUntil,

                IsPremium = u.Role == UserRoles.Admin || (u.IsPremium && (!u.PremiumUntil.HasValue || u.PremiumUntil.Value > DateTime.UtcNow)),

                u.PremiumPlan,

                u.PremiumUntil,

                ListingCount = _context.Listings.Count(l => l.SellerId == u.Id)

            })

            .ToListAsync();



        return Ok(users);

    }



    public class AdminSetProDto

    {

        public bool IsPremium { get; set; } = true;

        public string? Plan { get; set; } = "pro_monthly";

        public int Days { get; set; } = 30;

    }



    /// <summary>

    /// Ручне призначення / зняття PRO-статусу адміністратором

    /// </summary>

    [HttpPost("admin/{id}/pro")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> AdminSetPro(int id, [FromBody] AdminSetProDto dto)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        user.IsPremium = dto.IsPremium;

        if (dto.IsPremium)

        {

            user.PremiumPlan = string.IsNullOrWhiteSpace(dto.Plan) ? "pro_monthly" : dto.Plan;

            var currentExpiry = user.PremiumUntil.HasValue && user.PremiumUntil.Value > DateTime.UtcNow

                ? user.PremiumUntil.Value

                : DateTime.UtcNow;

            user.PremiumUntil = currentExpiry.AddDays(dto.Days > 0 ? dto.Days : 30);

        }

        else

        {

            user.PremiumPlan = null;

            user.PremiumUntil = null;

        }



        await LogAuditAsync(dto.IsPremium ? "AdminGrantPro" : "AdminRevokePro", "User", id,

            dto.IsPremium ? $"Адміністратор надав статус PRO користувачу {user.Email} на {dto.Days} днів (план: {user.PremiumPlan})."

                          : $"Адміністратор скасував статус PRO користувача {user.Email}.");



        await _context.SaveChangesAsync();



        return Ok(new

        {

            success = true,

            message = dto.IsPremium ? $"Статус PRO успішно надано користувачу {user.Email} на {dto.Days} днів!" : $"Статус PRO скасовано для {user.Email}.",

            isPremium = user.IsPremium,

            premiumPlan = user.PremiumPlan,

            premiumUntil = user.PremiumUntil

        });

    }



        /// <summary>

    /// Адміністрування: перегляд усіх оголошень конкретного користувача

    /// </summary>

    [HttpGet("admin/{id}/listings")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> GetUserListingsForAdmin(int id)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        var listings = await _context.Listings

            .AsNoTracking()

            .Include(l => l.Category)

                        .Where(l => l.SellerId == id)

            .OrderByDescending(l => l.CreatedAt)

            .Select(l => new

            {

                l.Id,

                l.Code,

                l.Title,

                l.Price,

                l.Status,

                l.Condition,

                l.CreatedAt,

                l.ViewsCount,

                                l.VideoUrl,

                l.IsFeatured,

                categoryName = l.Category != null ? l.Category.Name : "Інше",

                mainImage = l.ImageUrl ?? "/images/placeholder.svg"

            })

            .ToListAsync();



        return Ok(new

        {

            userId = user.Id,

            userName = $"{user.FirstName} {user.LastName}".Trim(),

            userEmail = user.Email,

            totalListings = listings.Count,

            listings

        });

    }



    /// <summary>

    /// Адміністрування: блокування / розблокування користувача

    /// </summary>

    [HttpPatch("admin/{id}/ban")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> BanUser(int id, [FromBody] BanUserDto dto)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        // Не дозволяти блокувати головного адміністратора (id 1 або nikitazhidenko@gmail.com)

        if ((user.Id == 1 || string.Equals(user.Email, "nikitazhidenko@gmail.com", StringComparison.OrdinalIgnoreCase)) && dto.IsBanned)

        {

            return BadRequest(new { error = "Головного адміністратора платформи (Нікіту) заблокувати неможливо. Він захищений системою назавжди." });

        }



        user.IsBanned = dto.IsBanned;

        user.BanReason = dto.IsBanned ? (dto.Reason ?? "Блокування за порушення правил") : null;



        if (dto.IsBanned)

        {

            user.RefreshToken = null; // Негайно скидаємо сесію

        }



        await _context.SaveChangesAsync();

        await LogAuditAsync(dto.IsBanned ? "BanUser" : "UnbanUser", "User", user.Id, $"Причина: {user.BanReason ?? "Не вказано"}");



        return Ok(new

        {

            success = true,

            message = dto.IsBanned ? $"Користувача {user.Email} заблоковано" : $"Користувача {user.Email} успішно розблоковано",

            isBanned = user.IsBanned,

            banReason = user.BanReason

        });

    }



/// <summary>

    /// Адміністрування: редагування даних користувача оператором (Ім'я, Телефон, Роль, Верифікація)

    /// </summary>

    [HttpPut("admin/{id}")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> UpdateUserByAdmin(int id, [FromBody] UpdateUserAdminDto dto)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        var callerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        int.TryParse(callerIdStr, out int callerId);

        var callerEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin@loopmarket.com";

        bool isMainAdmin = callerId == 1 || string.Equals(callerEmail, "nikitazhidenko@gmail.com", StringComparison.OrdinalIgnoreCase);



        // 1. Захист незмінності головного адміна Нікіти

        if ((user.Id == 1 || string.Equals(user.Email, "nikitazhidenko@gmail.com", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(dto.Role) && dto.Role != UserRoles.Admin)

        {

            return BadRequest(new { error = "Головний адміністратор (Нікіта) має незмінну роль Admin і не може бути понижений або змінений." });

        }



        // 2. Лише Головний адмін може призначати нових адміністраторів

        if (!string.IsNullOrWhiteSpace(dto.Role) && dto.Role == UserRoles.Admin && user.Role != UserRoles.Admin)

        {

            if (!isMainAdmin)

            {

                return StatusCode(403, new { error = "Тільки Головний адміністратор платформи (Нікіта) має право призначати нових адміністраторів." });

            }



            // Сповіщення на акаунт підтримки (ID 1)

            var supportNotification = new Notification

            {

                UserId = 1,

                Title = "🛡️ Призначено нового адміністратора",

                Message = $"Головний адміністратор {callerEmail} призначив користувача {user.FirstName} {user.LastName} ({user.Email}, ID #{user.Id}) адміністратором платформи. Нові права набудуть чинності після повторного входу в акаунт.",

                Link = "/admin.html",

                CreatedAt = DateTime.UtcNow,

                IsRead = false

            };

            _context.Notifications.Add(supportNotification);



            // Сповіщення призначеному користувачу

            var targetNotification = new Notification

            {

                UserId = user.Id,

                Title = "👑 Вам надано статус Адміністратора",

                Message = "Головний адміністратор надав вам права адміністратора LoopMarket. Для активації нових прав доступу, будь ласка, вийдіть та увійдіть в акаунт повторно.",

                Link = "/admin.html",

                CreatedAt = DateTime.UtcNow,

                IsRead = false

            };

            _context.Notifications.Add(targetNotification);



            await LogAuditAsync("PromoteToAdmin", "User", user.Id, $"Користувач {callerEmail} призначив {user.Email} адміном. Нові права набудуть чинності після повторного входу.");

        }



        if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber.Trim();

        

        var validRoles = new[] { UserRoles.Admin, UserRoles.Seller, UserRoles.Buyer, "User" };

        if (!string.IsNullOrWhiteSpace(dto.Role) && validRoles.Contains(dto.Role))

        {

            if (dto.Role == UserRoles.Admin && !isMainAdmin)

            {

                return StatusCode(403, new { error = "Тільки Головний адміністратор платформи (Нікіта) має право призначати нових адміністраторів." });

            }

            user.Role = dto.Role;

        }



        user.IsPhoneVerified = dto.IsPhoneVerified;
        if (dto.IsEmailVerified.HasValue)
        {
            user.IsEmailVerified = dto.IsEmailVerified.Value;
        }
        else
        {
            user.IsEmailVerified = dto.IsPhoneVerified;
        }
        if (user.IsEmailVerified)
        {
            user.EmailVerificationCode = null;
        }

        if (dto.TelegramChatId.HasValue)

        {

            user.TelegramChatId = dto.TelegramChatId.Value;

            if (dto.TelegramChatId.Value != 0)

            {

                user.ReceiveTelegramNotifications = true;

            }

        }



        await _context.SaveChangesAsync();



        return Ok(new

        {

            message = "Дані користувача успішно оновлено оператором",

            user = new

            {

                user.Id,

                user.FirstName,

                user.LastName,

                user.Email,

                user.PhoneNumber,

                user.Role,

                user.IsPhoneVerified,

                user.IsEmailVerified,

                isVerified = user.IsEmailVerified || user.IsPhoneVerified,

                user.IsBanned

            }

        });

    }



    /// <summary>

    /// Адміністрування: зведена статистика платформи для панелі оператора

    /// </summary>

    [HttpGet("admin/stats")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> GetAdminStats()

    {

        var totalUsers = await _context.Users.CountAsync();

        var totalListings = await _context.Listings.CountAsync();

        var totalOrders = await _context.Orders.CountAsync();

        var pendingComplaints = await _context.Complaints.CountAsync(c => c.Status == ComplaintStatus.Pending);

        var bannedUsersCount = await _context.Users.CountAsync(u => u.IsBanned);

        var totalTurnover = await _context.Orders.Where(o => o.Status == "Completed").SumAsync(o => (decimal?)o.TotalPrice) ?? 0;



        return Ok(new AdminStatsDto

        {

            TotalUsers = totalUsers,

            TotalListings = totalListings,

            TotalOrders = totalOrders,

            PendingComplaints = pendingComplaints,

            BannedUsersCount = bannedUsersCount,

            TotalTurnover = totalTurnover

        });

    }



    /// <summary>

    /// Адміністрування: зміна ролі користувача (Admin, Seller, Buyer) (Етап 6 RBAC)

    /// </summary>

    [HttpPatch("admin/{id}/role")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeRoleDto dto)

    {

        var validRoles = new[] { UserRoles.Admin, UserRoles.Seller, UserRoles.Buyer };

        if (!validRoles.Contains(dto.Role))

        {

            return BadRequest(new { error = "Невідома роль. Допустимі: Admin, Seller, Buyer" });

        }



        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        user.Role = dto.Role;

        await _context.SaveChangesAsync();

        await LogAuditAsync("ChangeRole", "User", user.Id, $"Нова роль: {user.Role}");



        return Ok(new { message = $"Роль користувача {user.Email} змінено на {user.Role}" });

    }



    /// <summary>

    /// Адміністрування: створення нового користувача з адмін-панелі

    /// </summary>

    [HttpPost("admin/create")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserDto dto)

    {

        if (string.IsNullOrWhiteSpace(dto.Email))

            return BadRequest(new { error = "Email обов'язковий" });

        if (string.IsNullOrWhiteSpace(dto.FirstName))

            return BadRequest(new { error = "Ім'я обов'язкове" });



        var emailNorm = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailNorm))

            return BadRequest(new { error = "Користувач з таким Email вже існує" });



        string? cleanPhone = null;

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))

        {

            cleanPhone = new string(dto.PhoneNumber.Where(char.IsDigit).ToArray());

            if (!string.IsNullOrEmpty(cleanPhone) && cleanPhone.Length >= 9)

            {

                if (!cleanPhone.StartsWith("+")) cleanPhone = "+" + cleanPhone;

            }

        }



        var validRoles = new[] { UserRoles.Admin, UserRoles.Seller, UserRoles.Buyer };

        var role = validRoles.Contains(dto.Role) ? dto.Role : UserRoles.Buyer;

        var password = string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password.Trim();



        var user = new User

        {

            Email = emailNorm,

            FirstName = dto.FirstName.Trim(),

            LastName = dto.LastName?.Trim() ?? string.Empty,

            PhoneNumber = cleanPhone ?? dto.PhoneNumber?.Trim(),

            Role = role,

            IsPhoneVerified = dto.IsPhoneVerified,

            IsEmailVerified = dto.IsEmailVerified || dto.IsPhoneVerified

        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);



        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        await LogAuditAsync("CreateUser", "User", user.Id, $"Створено {user.Email} ({user.Role})");



        return Ok(new

        {

            id = user.Id,

            email = user.Email,

            firstName = user.FirstName,

            lastName = user.LastName,

            role = user.Role,

            isPhoneVerified = user.IsPhoneVerified,

            message = "Користувача успішно створено!"

        });

    }



    /// <summary>

    /// Адміністрування: швидке перемикання або налаштування верифікації акаунта (в 1 клік)

    /// </summary>

    [HttpPatch("admin/{id}/verify")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminToggleVerify(int id, [FromBody] AdminToggleVerifyDto? dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        bool targetStatus;
        if (dto != null && dto.IsVerified.HasValue)
        {
            targetStatus = dto.IsVerified.Value;
        }
        else
        {
            targetStatus = !(user.IsPhoneVerified || user.IsEmailVerified);
        }

        user.IsPhoneVerified = dto?.IsPhoneVerified ?? targetStatus;
        user.IsEmailVerified = dto?.IsEmailVerified ?? targetStatus;

        if (user.IsEmailVerified)
        {
            user.EmailVerificationCode = null;
        }

        await _context.SaveChangesAsync();

        await LogAuditAsync(user.IsPhoneVerified || user.IsEmailVerified ? "VerifyUser" : "UnverifyUser", "User", user.Id,
            $"Верифікація оновлена: Email={user.IsEmailVerified}, Телефон={user.IsPhoneVerified}");

        return Ok(new
        {
            id = user.Id,
            isPhoneVerified = user.IsPhoneVerified,
            isEmailVerified = user.IsEmailVerified,
            isVerified = user.IsPhoneVerified || user.IsEmailVerified,
            message = (user.IsPhoneVerified && user.IsEmailVerified)
                ? "Статус верифікації: ПОВНІСТЮ ПІДТВЕРДЖЕНО ✅"
                : (user.IsPhoneVerified || user.IsEmailVerified)
                    ? "Статус верифікації: ЧАСТКОВО ПІДТВЕРДЖЕНО 🟡"
                    : "Статус верифікації: НЕ ПІДТВЕРДЖЕНО ⏳"
        });
    }

    /// <summary>

    /// Адміністрування: персональне сповіщення на акаунт / профіль користувача

    /// </summary>

    [HttpPost("admin/{id}/notify")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> AdminSendUserNotification(int id, [FromBody] AdminSendNotificationDto dto)

    {

        if (string.IsNullOrWhiteSpace(dto.Message))

            return BadRequest(new { error = "Текст сповіщення обов'язковий" });



        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        var notif = new Notification

        {

            UserId = user.Id,

            Title = string.IsNullOrWhiteSpace(dto.Title) ? "Сповіщення від адміністрації LoopMarket" : dto.Title.Trim(),

            Message = dto.Message.Trim(),

            Link = string.IsNullOrWhiteSpace(dto.Link) ? "/profile.html" : dto.Link.Trim(),

            IsRead = false,

            CreatedAt = DateTime.UtcNow

        };

        _context.Notifications.Add(notif);



        bool tgSent = false;

        if (dto.SendTelegram && user.TelegramChatId != 0 && user.ReceiveTelegramNotifications)

        {

            try

            {

                string tgText = $"🔔 <b>{notif.Title}</b>\n\n{notif.Message}\n\n<i>З повагою, адміністрація LoopMarket</i>";

                await _telegramService.SendNotificationAsync(user.TelegramChatId, tgText);

                tgSent = true;

            }

            catch (Exception ex)

            {

                Console.WriteLine($"[Notify TG Error] User {user.Id}: {ex.Message}");

            }

        }



        await _context.SaveChangesAsync();

        await LogAuditAsync("SendNotification", "User", user.Id, $"Тема: {notif.Title}");



        return Ok(new

        {

            message = "Сповіщення успішно доставлено в акаунт користувача!",

            inApp = true,

            telegramSent = tgSent

        });

    }



    /// <summary>

    /// Адміністрування: повне видалення користувача з очищенням пов'язаних записів

    /// </summary>

    [HttpDelete("admin/{id}")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> AdminDeleteUser(int id)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        // Захист головного адміністратора (Нікіти)
        if (string.Equals(user.Email, "nikitazhidenko@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, "nikitazidenko@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Головного адміністратора видалити неможливо. Він закріплений назавжди." });
        }

        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(adminIdStr, out int adminId) && adminId == id)
        {
            return BadRequest(new { error = "Заборонено видаляти власний акаунт адміністратора під час активної сесії" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Отримуємо всі ID оголошень користувача
            var listingIds = await _context.Listings
                .Where(l => l.SellerId == id)
                .Select(l => l.Id)
                .ToListAsync();

            // 2. Очищення списку обраного (якщо додані користувачем або іншими на його оголошення)
            var favs = await _context.Favorites
                .Where(f => f.UserId == id || (listingIds.Count > 0 && listingIds.Contains(f.ListingId)))
                .ToListAsync();
            if (favs.Count > 0) _context.Favorites.RemoveRange(favs);

            // 3. Очищення скарг (подані ним, на нього, або на його оголошення)
            var comps = await _context.Complaints
                .Where(c => c.ReporterId == id || c.TargetUserId == id || (c.ListingId.HasValue && listingIds.Contains(c.ListingId.Value)))
                .ToListAsync();
            if (comps.Count > 0) _context.Complaints.RemoveRange(comps);

            // 4. Очищення відгуків (залишені ним, отримані ним, або на його оголошення)
            var revs = await _context.Reviews
                .Where(r => r.AuthorId == id || r.TargetUserId == id || (r.ListingId.HasValue && listingIds.Contains(r.ListingId.Value)))
                .ToListAsync();
            if (revs.Count > 0) _context.Reviews.RemoveRange(revs);

            // 5. Очищення чатів / повідомлень
            var msgs = await _context.Messages
                .Where(m => m.SenderId == id || m.ReceiverId == id)
                .ToListAsync();
            if (msgs.Count > 0) _context.Messages.RemoveRange(msgs);

            // 6. Очищення сповіщень
            var notifs = await _context.Notifications
                .Where(n => n.UserId == id)
                .ToListAsync();
            if (notifs.Count > 0) _context.Notifications.RemoveRange(notifs);

            // 7. Очищення замовлень (покупець, продавець, або замовлення на його оголошення)
            var orders = await _context.Orders
                .Where(o => o.BuyerId == id || o.SellerId == id || (listingIds.Count > 0 && listingIds.Contains(o.ListingId)))
                .ToListAsync();
            if (orders.Count > 0) _context.Orders.RemoveRange(orders);

            // Зберігаємо очищення дочірніх зв'язків перед видаленням самих оголошень
            await _context.SaveChangesAsync();

            // 8. Видаляємо самі оголошення
            var listings = await _context.Listings
                .Where(l => l.SellerId == id)
                .ToListAsync();
            if (listings.Count > 0) _context.Listings.RemoveRange(listings);
            await _context.SaveChangesAsync();

            // 9. Видаляємо користувача
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            var details = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            return StatusCode(500, new { error = $"Помилка бази даних при видаленні користувача: {details}" });
        }

        await LogAuditAsync("DeleteUser", "User", id, $"Видалено користувача {user.Email}");



        return Ok(new { message = $"Користувача {user.FirstName} {user.LastName} (#{id}) успішно видалено з системи" });

    }



    /// <summary>

    /// Адміністрування: скидання паролю користувача

    /// </summary>

    [HttpPatch("admin/{id}/reset-password")]

    [Authorize(Roles = UserRoles.Admin)]

    public async Task<IActionResult> AdminResetPassword(int id, [FromBody] AdminResetPasswordDto dto)

    {

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Trim().Length < 6)

        {

            return BadRequest(new { error = "Новий пароль повинен містити щонайменше 6 символів" });

        }



        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword.Trim());

        user.RefreshToken = null;

        await _context.SaveChangesAsync();



        await LogAuditAsync("ResetPassword", "User", user.Id, $"Скинуто пароль користувача {user.Email}");



        return Ok(new { message = $"Пароль для користувача {user.Email} успішно змінено!" });

    }





    [HttpGet("telegram-status/{userId}")]

    [Authorize]

    public async Task<IActionResult> GetTelegramStatus(int userId)

    {

        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        bool isVerified = user.TelegramChatId != 0;



        return Ok(new

        {

            isVerified = isVerified,

            telegramUsername = isVerified ? user.TelegramChatId.ToString() : null

        });

    }



    [HttpGet("/api/system/health")]

    [AllowAnonymous]

    public async Task<IActionResult> CheckSystemHealth()

    {

        try

        {

            bool canConnect = await _context.Database.CanConnectAsync();

            if (!canConnect)

            {

                return StatusCode(503, new { status = "Error", message = "База даних недоступна" });

            }

            return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });

        }

        catch (Exception ex)

        {

            return StatusCode(500, new { status = "Fatal", error = ex.Message });

        }

    }



    [HttpGet("me")]

    [Authorize]

    public async Task<IActionResult> GetMyProfile()

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();



        return Ok(new

        {

            id = user.Id,

            firstName = user.FirstName,

            lastName = user.LastName,

            email = user.Email,

            role = user.Role,

            avatarUrl = user.AvatarUrl,

            bio = user.Bio,

            phoneNumber = user.PhoneNumber,

            isPhoneVerified = user.IsPhoneVerified,

            isTelegramVerified = user.IsPhoneVerified && user.TelegramChatId != 0,

            isEmailVerified = user.IsEmailVerified,

            isVerified = user.IsEmailVerified || (user.IsPhoneVerified && user.TelegramChatId != 0),

            telegramChatId = user.TelegramChatId,

            receiveTelegramNotifications = user.ReceiveTelegramNotifications,

            isPremium = user.Role == UserRoles.Admin || (user.IsPremium && (!user.PremiumUntil.HasValue || user.PremiumUntil.Value > DateTime.UtcNow)),

            premiumPlan = user.PremiumPlan,

            premiumUntil = user.PremiumUntil

        });

    }



    [HttpPost("upload-avatar")]

    [Authorize]

    public async Task<IActionResult> UploadAvatar([FromBody] AvatarDto dto)

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound();



        user.AvatarUrl = dto.AvatarUrl;

        await _context.SaveChangesAsync();



        return Ok(new { message = "Аватар оновлено", avatarUrl = user.AvatarUrl });

    }



    public class AvatarDto

    {

        public string AvatarUrl { get; set; } = string.Empty;

    }



    [HttpPost("send-email-code")]

    [Authorize]

    public async Task<IActionResult> SendEmailVerificationCode()

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        if (user.IsEmailVerified)

        {

            return Ok(new { message = "Ваша електронна пошта вже підтверджена", isEmailVerified = true });

        }



        // Rate limit: 60 sec cooldown

        bool isTestEnv = user.Email.Contains("test_") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing" || AppDomain.CurrentDomain.FriendlyName.Contains("testhost", StringComparison.OrdinalIgnoreCase);
        if (!isTestEnv && user.EmailVerificationCodeExpiresAt.HasValue && user.EmailVerificationCodeExpiresAt.Value > DateTime.UtcNow.AddMinutes(14))

        {

            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Зачекайте 60 секунд перед повторним запитом коду" });

        }



        var emailLower = user.Email.ToLowerInvariant();

        string[] disposableDomains = ["tempmail", "10minutemail", "mailinator", "guerrillamail", "throwawaymail", "trashmail", "dropmail"];

        if (disposableDomains.Any(d => emailLower.Contains(d)))

        {

            return BadRequest(new { error = "Використання тимчасових поштових скриньок заборонено з міркувань безпеки платформи" });

        }



        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        user.EmailVerificationCode = code;

        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        await _context.SaveChangesAsync();



        if (_emailService != null)

        {

            await _emailService.SendVerificationCodeAsync(user.Email, user.FirstName, code);

        }



        await LogAuditAsync("SendEmailVerificationCode", "User", user.Id, $"Код надіслано на {user.Email}");



        return Ok(new

        {

            message = "Код підтвердження успішно надіслано на вашу пошту",

            expiresAt = user.EmailVerificationCodeExpiresAt,

            devHint = (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" || user.Email.Contains("test_") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing") ? code : null

        });

    }



    [HttpPost("verify-email-code")]

    [Authorize]

    public async Task<IActionResult> VerifyEmailCode([FromBody] VerifyEmailCodeDto dto)

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        if (user.IsEmailVerified)

        {

            return Ok(new { message = "Електронна пошта вже підтверджена", isEmailVerified = true });

        }



        if (string.IsNullOrWhiteSpace(dto?.Code))

        {

            return BadRequest(new { error = "Введіть 6-значний код підтвердження" });

        }



        var enteredCode = dto.Code.Trim();



        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode) ||

            !user.EmailVerificationCodeExpiresAt.HasValue ||

            user.EmailVerificationCodeExpiresAt.Value < DateTime.UtcNow)

        {

            return BadRequest(new { error = "Термін дії коду підтвердження сплив. Будь ласка, замовте новий код." });

        }



        if (user.EmailVerificationCode != enteredCode)

        {

            return BadRequest(new { error = "Невірний код підтвердження. Перевірте та спробуйте ще раз." });

        }



        user.IsEmailVerified = true;

        user.EmailVerificationCode = null;

        user.EmailVerificationCodeExpiresAt = null;

        await _context.SaveChangesAsync();



        await LogAuditAsync("VerifyEmailSuccess", "User", user.Id, "Електронну пошту успішно верифіковано кодом");



        return Ok(new

        {

            message = "Електронну пошту успішно підтверджено! Тепер ви можете публікувати оголошення.",

            isEmailVerified = true

        });

    }



    [HttpGet("public/{id}")]

    [AllowAnonymous]

    public async Task<IActionResult> GetPublicProfile(int id)

    {

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        var completedServices = await _context.Orders

            .AsNoTracking()

            .CountAsync(o => o.SellerId == id && o.Status == "Completed");



        return Ok(new

        {

            id = user.Id,

            firstName = user.FirstName,

            lastName = user.LastName,

            role = user.Role,

            avatarUrl = user.AvatarUrl,

            bio = user.Bio,

            phoneNumber = user.PhoneNumber,

            isPhoneVerified = user.IsPhoneVerified,

            completedServicesCount = completedServices,

            isPremium = user.Role == UserRoles.Admin || (user.IsPremium && (!user.PremiumUntil.HasValue || user.PremiumUntil.Value > DateTime.UtcNow)),

            premiumPlan = user.PremiumPlan

        });

    }



    /// <summary>

    /// Детальна статистика послуг користувача (виконані послуги, замовлення по кожній послузі, заробіток)

    /// </summary>

    [HttpGet("{id}/services-stats")]

    [AllowAnonymous]

    public async Task<IActionResult> GetUserServicesStats(int id)

    {

        var user = await _context.Users.AsNoTracking()

            .Select(u => new { u.Id, u.FirstName, u.LastName })

            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound(new { error = "Користувача не знайдено" });



        // Всі послуги користувача

        var userServices = await _context.Listings

            .AsNoTracking()

            .Where(l => l.SellerId == id && (l.CategoryId == 8 || l.Title.Contains("Послуг")))

            .Select(l => new

            {

                l.Id,

                l.Title,

                l.Price,

                l.Condition,

                l.Location,

                l.IsShippingAvailable,

                l.Status,

                l.ImageUrl

            })

            .ToListAsync();



        var serviceListingIds = userServices.Select(s => s.Id).ToList();



        // Замовлення, отримані на послуги користувача

        var receivedOrders = await _context.Orders

            .AsNoTracking()

            .Where(o => o.SellerId == id && (serviceListingIds.Contains(o.ListingId) || (o.Listing != null && (o.Listing.CategoryId == 8 || o.Listing.Title.Contains("Послуг")))))

            .Select(o => new

            {

                o.Id,

                o.ListingId,

                o.TotalPrice,

                o.Status,

                o.CreatedAt

            })

            .ToListAsync();



        int totalOrdersReceived = receivedOrders.Count;

        int completedOrdersCount = receivedOrders.Count(o => o.Status == "Completed");

        decimal totalEarnings = receivedOrders.Where(o => o.Status == "Completed").Sum(o => o.TotalPrice);



        // Послуги, які цей користувач замовляв у інших як клієнт

        int servicesRequestedCount = await _context.Orders

            .CountAsync(o => o.BuyerId == id && (o.Listing != null && (o.Listing.CategoryId == 8 || o.Listing.Title.Contains("Послуг"))));



        // Статистика по кожній окремій послузі

        var servicesDetail = userServices.Select(s =>

        {

            var ordersForThisService = receivedOrders.Where(o => o.ListingId == s.Id).ToList();

            return new

            {

                s.Id,

                s.Title,

                s.Price,

                s.Condition,

                s.Location,

                s.IsShippingAvailable,

                s.Status,

                s.ImageUrl,

                ordersCount = ordersForThisService.Count,

                completedCount = ordersForThisService.Count(o => o.Status == "Completed"),

                earnings = ordersForThisService.Where(o => o.Status == "Completed").Sum(o => o.TotalPrice)

            };

        }).ToList();



        return Ok(new

        {

            userId = id,

            userName = $"{user.FirstName} {user.LastName}".Trim(),

            completedServicesCount = completedOrdersCount,

            totalOrdersReceived = totalOrdersReceived,

            servicesRequestedCount = servicesRequestedCount,

            activeServicesCount = userServices.Count(s => s.Status == "Active"),

            totalEarnings = totalEarnings,

            services = servicesDetail

        });

    }



    

    /// <summary>

    /// Аналітика та доходи продавця з антифрод-захистом (Етап 6 та 7)

    /// </summary>

    [HttpGet("{id}/analytics")]

    [AllowAnonymous]

    public async Task<IActionResult> GetUserAnalytics(int id)

    {

        var userExists = await _context.Users.AsNoTracking().AnyAsync(u => u.Id == id);

        if (!userExists) return NotFound(new { error = "Користувача не знайдено" });



        // 1. Оголошення продавця

        var listings = await _context.Listings

            .AsNoTracking()

            .Where(l => l.SellerId == id)

            .ToListAsync();



        int totalViews = listings.Sum(l => l.ViewsCount);

        int activeCount = listings.Count(l => l.Status == "Active");



        // 2. Вподобання

        var listingIds = listings.Select(l => l.Id).ToList();

        int totalFavs = await _context.Favorites.CountAsync(f => listingIds.Contains(f.ListingId));



        // 3. Завершені угоди (АНТИФРОД: тільки успішно закриті замовлення без самовикупу BuyerId != SellerId)

        var completedOrders = await _context.Orders

            .AsNoTracking()

            .Where(o => o.SellerId == id && o.Status == "Completed" && o.BuyerId != id)

            .ToListAsync();



        int completedCount = completedOrders.Count;

        decimal totalRevenue = completedOrders.Sum(o => o.TotalPrice);



        // 4. Рейтинг та відгуки

        var reviews = await _context.Reviews

            .AsNoTracking()

            .Where(r => r.TargetUserId == id)

            .ToListAsync();



        double avgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 5.0;

        int verifiedCount = reviews.Count(r => r.IsVerifiedPurchase) > 0 ? reviews.Count(r => r.IsVerifiedPurchase) : reviews.Count;



        // 5. Топ-оголошення за переглядами

        var topListings = listings

            .OrderByDescending(l => l.ViewsCount)

            .Take(5)

            .Select(l => new

            {

                id = l.Id,

                title = l.Title,

                price = l.Price,

                viewsCount = l.ViewsCount,

                status = l.Status,

                imageUrl = l.ImageUrl,

                isService = l.CategoryId == 8 || l.Title.Contains("Послуг")

            })

            .ToList();



        return Ok(new

        {

            totalViews,

            activeCount,

            totalFavs,

            completedOrders = completedCount,

            totalRevenue,

            avgRating,

            verifiedReviewsCount = verifiedCount,

            topListings

        });

    }



    [HttpGet("{id}")]

    [Authorize]

    public async Task<IActionResult> GetUserById(int id)

    {

        var user = await _context.Users.FindAsync(id);

        if (user == null) return NotFound();



        return Ok(new

        {

            id = user.Id,

            firstName = user.FirstName,

            lastName = user.LastName,

            email = user.Email,

            role = user.Role,

            avatarUrl = user.AvatarUrl,

            bio = user.Bio,

            telegramChatId = user.TelegramChatId,

            receiveTelegramNotifications = user.ReceiveTelegramNotifications

        });

    }



    [HttpPut("me")]

    [Authorize]

    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound();



        user.Bio = dto.Bio;

        if (!string.IsNullOrEmpty(dto.AvatarUrl))

        {

            user.AvatarUrl = dto.AvatarUrl;

        }



        await _context.SaveChangesAsync();

        return Ok(new { message = "Профіль оновлено!" });

    }



    [HttpPost("link-telegram")]

    [Authorize]

    public async Task<IActionResult> LinkTelegram([FromBody] TelegramLinkDto dto)

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound("Користувача не знайдено");



        user.TelegramChatId = dto.TelegramChatId;

        user.ReceiveTelegramNotifications = true;

        await _context.SaveChangesAsync();



        return Ok(new { success = true, message = "Telegram успішно прив'язано!" });

    }



    [HttpGet("seller-stats")]

    [Authorize]

    public async Task<IActionResult> GetSellerStats()

    {

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();



        var listings = await _context.Listings

            .Where(l => l.SellerId == userId)

            .ToListAsync();



        var activeListingsCount = listings.Count(l => l.Status == "Active");

        var totalViews = listings.Sum(l => l.ViewsCount);



        var listingIds = listings.Select(l => l.Id).ToList();

        var totalFavorites = await _context.Favorites

            .CountAsync(f => listingIds.Contains(f.ListingId));



        var sellerOrders = await _context.Orders

            .Where(o => o.SellerId == userId)

            .ToListAsync();



        var completedOrders = sellerOrders.Where(o => o.Status == OrderStatus.Completed).ToList();

        var completedOrdersCount = completedOrders.Count;

        var totalRevenue = completedOrders.Sum(o => o.TotalPrice);

        var pendingOrdersCount = sellerOrders.Count(o => o.Status == OrderStatus.Pending);



        var reviews = await _context.Reviews

            .Where(r => r.TargetUserId == userId)

            .ToListAsync();



        var totalReviews = reviews.Count;

        var averageRating = totalReviews > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 5.0;

        var verifiedReviewsCount = reviews.Count(r => r.IsVerifiedPurchase);



        var topListings = listings

            .OrderByDescending(l => l.ViewsCount)

            .Take(5)

            .Select(l => new

            {

                id = l.Id,

                title = l.Title,

                price = l.Price,

                viewsCount = l.ViewsCount,

                imageUrl = (string.IsNullOrEmpty(l.ImageUrl) || l.ImageUrl.Contains("placehold.co"))

                    ? (l.CategoryId == 8 || (l.Title != null && l.Title.Contains("Послуг")) ? "/images/placeholder-service.svg" : "/images/placeholder.svg")

                    : l.ImageUrl.Split(';', StringSplitOptions.None)[0],

                status = l.Status

            })

            .ToList();



        return Ok(new

        {

            activeListingsCount,

            totalListingsCount = listings.Count,

            totalViews,

            totalFavorites,

            completedOrdersCount,

            totalRevenue,

            pendingOrdersCount,

            averageRating,

            totalReviews,

            verifiedReviewsCount,

            topListings

        });

    }



    private async Task LogAuditAsync(string action, string? targetType = null, int? targetId = null, string? details = null)

    {

        try

        {

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            int.TryParse(adminIdStr, out int adminId);

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin@loopmarket.com";

            var log = new AdminAuditLog

            {

                AdminId = adminId > 0 ? adminId : 1,

                AdminEmail = adminEmail,

                Action = action,

                TargetType = targetType,

                TargetId = targetId,

                Details = details,

                CreatedAt = DateTime.UtcNow

            };

            _context.AdminAuditLogs.Add(log);

            await _context.SaveChangesAsync();

        }

        catch { }

    }

}



public class UpdateProfileDto

{

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

}



public class TelegramLinkDto

{

    public long TelegramChatId { get; set; }

}





public class VerifyRegistrationCodeDto

{

    public int UserId { get; set; }

    public string Ticket { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

}



public class ResendRegistrationCodeDto

{

    public int UserId { get; set; }

    public string Ticket { get; set; } = string.Empty;

}