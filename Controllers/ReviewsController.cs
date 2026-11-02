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
                .Include(r => r.Author)
                .Include(r => r.Listing)
                .Select(r => new
                {
                    id = r.Id,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    author = r.Author != null ? new
                    {
                        id = r.Author.Id,
                        firstName = r.Author.FirstName,
                        lastName = r.Author.LastName,
                        avatarUrl = r.Author.AvatarUrl
                    } : null,
                    listing = r.Listing != null ? new
                    {
                        id = r.Listing.Id,
                        title = r.Listing.Title,
                        imageUrl = r.Listing.ImageUrl
                    } : null
                })
                .ToListAsync();

            double averageRating = reviews.Any() ? reviews.Average(r => r.rating) : 0;

            return Ok(new
            {
                averageRating = Math.Round(averageRating, 1),
                totalReviews = reviews.Count,
                reviews
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

            if (dto.ListingId.HasValue)
            {
                var listingExists = await _context.Listings.AnyAsync(l => l.Id == dto.ListingId.Value);
                if (!listingExists)
                {
                    return BadRequest(new { error = "Оголошення не знайдено" });
                }
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
