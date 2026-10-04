using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserReviews(int userId)
    {
        try
        {
            var reviews = await _context.Reviews
                .Where(r => r.TargetUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            double averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

            var resultList = new List<object>();

            foreach (var r in reviews)
            {
                var author = await _context.Users.FindAsync(r.AuthorId);
                var listing = r.ListingId.HasValue ? await _context.Listings.FindAsync(r.ListingId.Value) : null;

                resultList.Add(new
                {
                    id = r.Id,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    author = author != null ? new { id = author.Id, firstName = author.FirstName, lastName = author.LastName, avatarUrl = author.AvatarUrl } : null,
                    listing = listing != null ? new { id = listing.Id, title = listing.Title, imageUrl = listing.ImageUrl } : null
                });
            }

            return Ok(new
            {
                averageRating = Math.Round(averageRating, 1),
                totalReviews = reviews.Count,
                reviews = resultList
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int authorId))
                return Unauthorized(new { error = "Не авторизовано" });

            if (authorId == dto.TargetUserId)
            {
                return BadRequest(new { error = "Ви не можете залишити відгук самому собі" });
            }

            var targetUser = await _context.Users.FindAsync(dto.TargetUserId);
            if (targetUser == null)
            {
                return NotFound(new { error = "Користувача не знайдено" });
            }

            var review = new Review
            {
                TargetUserId = dto.TargetUserId,
                AuthorId = authorId,
                ListingId = dto.ListingId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Відгук успішно додано!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class CreateReviewDto
    {
        public int TargetUserId { get; set; }
        public int? ListingId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}