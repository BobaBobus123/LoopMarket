using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public UsersController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { error = "Email вже використовується" });

        var cleanPhone = new string(dto.PhoneNumber.Where(char.IsDigit).ToArray());
        var allUsers = await _context.Users.ToListAsync();
        bool phoneExists = allUsers.Any(u => u.PhoneNumber != null && new string(u.PhoneNumber.Where(char.IsDigit).ToArray()) == cleanPhone);

        if (phoneExists)
            return BadRequest(new { error = "Цей номер телефону вже зареєстрований!" });

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = cleanPhone,
            PasswordHash = dto.Password,
            Role = "User"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(user);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.PasswordHash == dto.Password);
        if (user == null) return Unauthorized(new { error = "Невірний email або пароль" });

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: credentials);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            isPhoneVerified = user.IsPhoneVerified
        });
    }
    [HttpGet("telegram-status/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetTelegramStatus(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "Користувача не знайдено" });

        // Если у пользователя уже заполнено поле TelegramId или флаг верификации
        bool isVerified = !string.IsNullOrEmpty(user.TelegramChatId);

        return Ok(new
        {
            isVerified = isVerified,
            telegramUsername = user.TelegramUsername ?? "user"
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

        user.AvatarUrl = dto.AvatarUrl; // Здесь сохраняется Base64 или ссылка на картинку
        await _context.SaveChangesAsync();

        return Ok(new { message = "Аватар оновлено", avatarUrl = user.AvatarUrl });
    }

    public class AvatarDto
    {
        public string AvatarUrl { get; set; }
    }
    // 🔥 ДОБАВЛЕННЫЙ МЕТОД ДЛЯ ПОЛУЧЕНИЯ ПОЛЬЗОВАТЕЛЯ ПО ID (чтобы работала кнопка проверки статуса)
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