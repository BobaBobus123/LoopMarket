using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;

namespace TradeStorm.Api.Controllers;

[Authorize]
[Route("api/notifications")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications([FromQuery] bool wrapped = false)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var notifs = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .ToListAsync();

        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        Response.Headers["X-Unread-Count"] = unreadCount.ToString();

        if (wrapped)
        {
            return Ok(new { unreadCount, items = notifs });
        }

        return Ok(notifs);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var count = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Позначено як прочитані" });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkOneAsRead(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notif != null)
        {
            notif.IsRead = true;
            await _context.SaveChangesAsync();
        }
        return Ok(new { success = true });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notif != null)
        {
            _context.Notifications.Remove(notif);
            await _context.SaveChangesAsync();
        }
        return Ok(new { success = true });
    }
}
