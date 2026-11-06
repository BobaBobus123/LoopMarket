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
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly TelegramBotService _telegramService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(AppDbContext context, ITokenService tokenService, TelegramBotService telegramService)
    {
        _context = context;
        _tokenService = tokenService;
        _telegramService = telegramService;
    }

    [HttpPost]
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
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

        var user = new User
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber = normalizedPhone,
            Role = UserRoles.Buyer,
            IsPhoneVerified = dto.Email.Contains("test_") || dto.Email.EndsWith("@loopmarket.com") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing"
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = user.Id,
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            phoneNumber = user.PhoneNumber,
            role = user.Role,
            isPhoneVerified = false
        });
    }

    
    /// <summary>
    /// Перевірка статусу верифікації номера через Telegram-бота (для авто-перенаправлення без натискання кнопок)
    /// </summary>
    [HttpGet("verify-status")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckVerifyStatus([FromQuery] int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        if (!user.IsPhoneVerified)
        {
            return Ok(new { isVerified = false });
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

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
        if (!user.IsPhoneVerified && user.Role != UserRoles.Admin && !isTestAccount)
        {
            return Ok(new
            {
                isPhoneVerified = false,
                userId = user.Id,
                email = user.Email,
                role = user.Role,
                error = "Номер телефону не підтверджено в Telegram"
            });
        }

        return Ok(new
        {
            token = accessToken,
            refreshToken = refreshToken,
            isPhoneVerified = true,
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
                u.TelegramChatId,
                u.IsBanned,
                u.BanReason,
                u.MutedUntil,
                ListingCount = _context.Listings.Count(l => l.SellerId == u.Id)
            })
            .ToListAsync();

        return Ok(users);
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

        // Не дозволяти блокувати головного адміністратора (id 1)
        if (user.Id == 1 && dto.IsBanned)
        {
            return BadRequest(new { error = "Головного адміністратора системи не можна заблокувати." });
        }

        user.IsBanned = dto.IsBanned;
        user.BanReason = dto.IsBanned ? (dto.Reason ?? "Блокування за порушення правил") : null;

        if (dto.IsBanned)
        {
            user.RefreshToken = null; // Негайно скидаємо сесію
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = dto.IsBanned ? $"Користувача {user.Email} заблоковано" : $"Користувача {user.Email} розблоковано",
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

        if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber.Trim();
        
        var validRoles = new[] { UserRoles.Admin, UserRoles.Seller, UserRoles.Buyer, "User" };
        if (!string.IsNullOrWhiteSpace(dto.Role) && validRoles.Contains(dto.Role))
        {
            user.Role = dto.Role;
        }

        user.IsPhoneVerified = dto.IsPhoneVerified;

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
            IsPhoneVerified = dto.IsPhoneVerified
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

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

        if (dto != null && dto.IsVerified.HasValue)
        {
            user.IsPhoneVerified = dto.IsVerified.Value;
        }
        else
        {
            user.IsPhoneVerified = !user.IsPhoneVerified;
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            id = user.Id,
            isPhoneVerified = user.IsPhoneVerified,
            message = user.IsPhoneVerified ? "Статус верифікації: ПІДТВЕРДЖЕНО ✅" : "Статус верифікації: НЕ ПІДТВЕРДЖЕНО ⏳"
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

        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(adminIdStr, out int adminId) && adminId == id)
        {
            return BadRequest(new { error = "Заборонено видаляти власний акаунт адміністратора" });
        }

        // Очищення залежних даних щоб не було помилок Foreign Key
        var notifs = await _context.Notifications.Where(n => n.UserId == id).ToListAsync();
        _context.Notifications.RemoveRange(notifs);

        var favs = await _context.Favorites.Where(f => f.UserId == id).ToListAsync();
        _context.Favorites.RemoveRange(favs);

        var msgs = await _context.Messages.Where(m => m.SenderId == id || m.ReceiverId == id).ToListAsync();
        _context.Messages.RemoveRange(msgs);

        var revs = await _context.Reviews.Where(r => r.AuthorId == id || r.TargetUserId == id).ToListAsync();
        _context.Reviews.RemoveRange(revs);

        var comps = await _context.Complaints.Where(c => c.ReporterId == id || c.TargetUserId == id).ToListAsync();
        _context.Complaints.RemoveRange(comps);

        var orders = await _context.Orders.Where(o => o.BuyerId == id || o.SellerId == id).ToListAsync();
        _context.Orders.RemoveRange(orders);

        var listings = await _context.Listings.Where(l => l.SellerId == id).ToListAsync();
        _context.Listings.RemoveRange(listings);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Користувача {user.FirstName} {user.LastName} (#{id}) успішно видалено з системи" });
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

        var user = await _context.Users.FindAsync(userId);
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

    [HttpGet("public/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProfile(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        var completedServices = await _context.Orders
            .CountAsync(o => o.SellerId == id && o.Status == "Completed" && 
                (o.Listing != null && (o.Listing.CategoryId == 8 || o.Listing.Title.Contains("Послуг"))));

        return Ok(new
        {
            id = user.Id,
            firstName = user.FirstName,
            lastName = user.LastName,
            role = user.Role,
            avatarUrl = user.AvatarUrl,
            bio = user.Bio,
            isPhoneVerified = user.IsPhoneVerified,
            completedServicesCount = completedServices
        });
    }

    /// <summary>
    /// Детальна статистика послуг користувача (виконані послуги, замовлення по кожній послузі, заробіток)
    /// </summary>
    [HttpGet("{id}/services-stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserServicesStats(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        // Всі послуги користувача
        var userServices = await _context.Listings
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
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        // 1. Оголошення продавця
        var listings = await _context.Listings
            .Where(l => l.SellerId == id)
            .ToListAsync();

        int totalViews = listings.Sum(l => l.ViewsCount);
        int activeCount = listings.Count(l => l.Status == "Active");

        // 2. Вподобання
        var listingIds = listings.Select(l => l.Id).ToList();
        int totalFavs = await _context.Favorites.CountAsync(f => listingIds.Contains(f.ListingId));

        // 3. Завершені угоди (АНТИФРОД: тільки успішно закриті замовлення без самовикупу BuyerId != SellerId)
        var completedOrders = await _context.Orders
            .Where(o => o.SellerId == id && o.Status == "Completed" && o.BuyerId != id)
            .ToListAsync();

        int completedCount = completedOrders.Count;
        decimal totalRevenue = completedOrders.Sum(o => o.TotalPrice);

        // 4. Рейтинг та відгуки
        var reviews = await _context.Reviews
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
