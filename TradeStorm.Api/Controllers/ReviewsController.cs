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
                    isVerifiedPurchase = r.IsVerifiedPurchase,
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
                verifiedReviewsCount = reviews.Count(r => r.isVerifiedPurchase),
                reviews
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("listing/{listingId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingReviews(int listingId)
    {
        try
        {
            var reviews = await _context.Reviews
                .Where(r => r.ListingId == listingId)
                .OrderByDescending(r => r.CreatedAt)
                .Include(r => r.Author)
                .Select(r => new
                {
                    id = r.Id,
                    rating = r.Rating,
                    comment = r.Comment,
                    isVerifiedPurchase = r.IsVerifiedPurchase,
                    createdAt = r.CreatedAt,
                    author = r.Author != null ? new
                    {
                        id = r.Author.Id,
                        firstName = r.Author.FirstName,
                        lastName = r.Author.LastName,
                        avatarUrl = r.Author.AvatarUrl
                    } : null
                })
                .ToListAsync();

            double averageRating = reviews.Any() ? reviews.Average(r => r.rating) : 0;

            return Ok(new
            {
                averageRating = Math.Round(averageRating, 1),
                totalReviews = reviews.Count,
                verifiedReviewsCount = reviews.Count(r => r.isVerifiedPurchase),
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

            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new { error = "Оцінка має бути від 1 до 5 зірок" });
            }

            if (dto.ListingId.HasValue)
            {
                var listingExists = await _context.Listings.AnyAsync(l => l.Id == dto.ListingId.Value);
                if (!listingExists)
                {
                    return BadRequest(new { error = "Оголошення не знайдено" });
                }

                // Перевірка на повторний відгук до одного й того ж товару
                var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.AuthorId == authorId && r.ListingId == dto.ListingId.Value);
                if (alreadyReviewed)
                {
                    return BadRequest(new { error = "Ви вже залишили відгук до цієї угоди" });
                }
            }

            // Перевіряємо, чи є підтверджена угода через Loop Захист
            bool isVerified = false;
            if (dto.ListingId.HasValue)
            {
                isVerified = await _context.Orders.AnyAsync(o =>
                    o.BuyerId == authorId &&
                    o.ListingId == dto.ListingId.Value &&
                    o.Status == OrderStatus.Completed);
            }
            if (!isVerified)
            {
                // Також перевіряємо, чи була будь-яка успішна угода між цим покупцем і продавцем
                isVerified = await _context.Orders.AnyAsync(o =>
                    o.BuyerId == authorId &&
                    o.SellerId == dto.TargetUserId &&
                    o.Status == OrderStatus.Completed);
            }

            var author = await _context.Users.FindAsync(authorId);
            var authorName = author != null ? $"{author.FirstName} {author.LastName}".Trim() : "Покупець";

            var review = new Review
            {
                TargetUserId = dto.TargetUserId,
                AuthorId = authorId,
                ListingId = dto.ListingId,
                Rating = dto.Rating,
                Comment = dto.Comment?.Trim(),
                IsVerifiedPurchase = isVerified,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);

            // Створюємо сповіщення для продавця
            var notif = new Notification
            {
                UserId = dto.TargetUserId,
                Title = isVerified ? "🛡️ Новий перевірений відгук (Loop Захист)!" : "⭐ Новий відгук про вас!",
                Message = $"{authorName} оцінив(ла) вас на {dto.Rating}/5 зірок" + (!string.IsNullOrWhiteSpace(dto.Comment) ? $": \"{dto.Comment}\"" : "."),
                Link = "/profile.html#reviews",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notif);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isVerified
                    ? "Дякуємо! Відгук опубліковано з відзнакою '🛡️ Перевірена угода (Loop Захист)'"
                    : "Відгук успішно додано!",
                isVerifiedPurchase = isVerified
            });
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
