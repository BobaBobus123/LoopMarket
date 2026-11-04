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
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { error = "Email вже використовується" });

        var cleanPhone = new string(dto.PhoneNumber.Where(char.IsDigit).ToArray());
        bool phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber == dto.PhoneNumber);

        if (phoneExists)
            return BadRequest(new { error = "Цей номер телефону вже зареєстрований!" });

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = cleanPhone,
            Role = UserRoles.Buyer // За замовчуванням новий користувач - Покупець (Buyer)
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
            role = user.Role
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

        return Ok(new
        {
            token = accessToken,
            refreshToken = refreshToken,
            isPhoneVerified = user.IsPhoneVerified,
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
