using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/complaints")]
public class ComplaintsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ComplaintsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Подання скарги на користувача чи підозріле оголошення
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int currentUserId))
        {
            return Unauthorized();
        }

        if (currentUserId == dto.TargetUserId)
        {
            return BadRequest(new { error = "Ви не можете подати скаргу на самого себе." });
        }

        var targetUser = await _context.Users.FindAsync(dto.TargetUserId);
        if (targetUser == null)
        {
            return NotFound(new { error = "Користувача, на якого подається скарга, не знайдено." });
        }

        var complaint = new Complaint
        {
            ReporterId = currentUserId,
            TargetUserId = dto.TargetUserId,
            ListingId = dto.ListingId,
            Reason = dto.Reason,
            Details = dto.Details,
            Status = ComplaintStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Дякуємо! Вашу скаргу успішно зареєстровано та передано модерації LoopMarket.",
            id = complaint.Id
        });
    }

    /// <summary>
    /// Адміністрування: перегляд усіх скарг платформи для оператора безпеки
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAllComplaints([FromQuery] string? status)
    {
        var query = _context.Complaints
            .Include(c => c.Reporter)
            .Include(c => c.TargetUser)
            .Include(c => c.Listing)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(c => c.Status == status);
        }

        var rawList = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                id = c.Id,
                reporterId = c.ReporterId,
                reporterName = c.Reporter != null ? $"{c.Reporter.FirstName} {c.Reporter.LastName}".Trim() : "Анонім",
                reporterEmail = c.Reporter != null ? c.Reporter.Email : string.Empty,
                targetUserId = c.TargetUserId,
                targetUserName = c.TargetUser != null ? $"{c.TargetUser.FirstName} {c.TargetUser.LastName}".Trim() : "Користувач",
                targetUserEmail = c.TargetUser != null ? c.TargetUser.Email : string.Empty,
                targetUserPhone = c.TargetUser != null ? c.TargetUser.PhoneNumber : string.Empty,
                isTargetUserBanned = c.TargetUser != null && c.TargetUser.IsBanned,
                listingId = c.ListingId,
                listingTitle = c.Listing != null ? c.Listing.Title : null,
                reason = c.Reason,
                details = c.Details,
                status = c.Status,
                createdAt = c.CreatedAt,
                resolutionNotes = c.ResolutionNotes
            })
            .ToListAsync();

        var list = rawList.Select(c => new
        {
            c.id,
            c.reporterId,
            c.reporterName,
            c.reporterEmail,
            c.targetUserId,
            c.targetUserName,
            c.targetUserEmail,
            targetUserPhone = TradeStorm.Api.Services.PhoneNumberHelper.Normalize(c.targetUserPhone) ?? string.Empty,
            c.isTargetUserBanned,
            c.listingId,
            c.listingTitle,
            c.reason,
            c.details,
            c.status,
            c.createdAt,
            c.resolutionNotes
        }).ToList();

        return Ok(list);
    }

    /// <summary>
    /// Адміністрування: вирішення скарги, відхилення або миттєвий бан порушника
    /// </summary>
    [HttpPatch("admin/{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ResolveComplaint(int id, [FromBody] ResolveComplaintDto dto)
    {
        var complaint = await _context.Complaints
            .Include(c => c.TargetUser)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null)
        {
            return NotFound(new { error = "Скаргу не знайдено." });
        }

        complaint.Status = dto.Status;
        complaint.ResolutionNotes = dto.ResolutionNotes;

        if (dto.BanTargetUser && complaint.TargetUser != null)
        {
            complaint.TargetUser.IsBanned = true;
            complaint.TargetUser.BanReason = !string.IsNullOrWhiteSpace(dto.BanReason)
                ? dto.BanReason
                : $"Заблоковано за скаргою #{id}: {complaint.Reason}";
            complaint.TargetUser.RefreshToken = null;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Скаргу опрацьовано",
            status = complaint.Status,
            targetUserBanned = complaint.TargetUser?.IsBanned ?? false
        });
    }
}
