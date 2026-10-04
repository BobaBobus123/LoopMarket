using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ListingsController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetListings(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? condition)
    {
        var query = _context.Listings
            .Include(l => l.Seller)
            .Include(l => l.Category)
            .AsQueryable();

        // Фильтрация по поисковому запросу (название или описание)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.Title.ToLower().Contains(search.ToLower()) ||
                                   (l.Description != null && l.Description.ToLower().Contains(search.ToLower())));
        }

        // Фильтрация по категории
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(l => l.CategoryId == categoryId.Value);
        }

        // Фильтрация по минимальной цене
        if (minPrice.HasValue)
        {
            query = query.Where(l => l.Price >= minPrice.Value);
        }

        // Фильтрация по максимальной цене
        if (maxPrice.HasValue)
        {
            query = query.Where(l => l.Price <= maxPrice.Value);
        }

        // Фильтрация по состоянию (Нове / Вживане)
        if (!string.IsNullOrWhiteSpace(condition))
        {
            query = query.Where(l => l.Condition == condition);
        }

        var listings = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                id = l.Id,
                title = l.Title,
                description = l.Description,
                price = l.Price,
                imageUrl = string.IsNullOrEmpty(l.ImageUrl) ? null : l.ImageUrl.Split(';', StringSplitOptions.None)[0],
                condition = l.Condition,
                location = l.Location,
                isShippingAvailable = l.IsShippingAvailable,
                categoryName = l.Category != null ? l.Category.Name : null,
                categoryId = l.CategoryId,
                sellerId = l.SellerId,
                seller = l.Seller != null ? new { firstName = l.Seller.FirstName, lastName = l.Seller.LastName, avatarUrl = l.Seller.AvatarUrl } : null
            })
            .ToListAsync();

        return Ok(new { listings });
    }

    // МЕТОД ДЛЯ МНОЖЕСТВА ФОТО
    [HttpPost("upload-multiple")]
    [Authorize]
    public async Task<IActionResult> UploadMultipleImages([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "Файли не обрано" });

        string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string uploadsFolder = Path.Combine(webRootPath, "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var uploadedUrls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length > 0 && file.ContentType.StartsWith("image/"))
            {
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName.Replace(" ", "_");
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                uploadedUrls.Add($"/uploads/{uniqueFileName}");
            }
        }

        return Ok(new { imageUrls = uploadedUrls });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized(new { error = "Невірний токен" });

        var listing = new Listing
        {
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrls != null && dto.ImageUrls.Any() ? string.Join(";", dto.ImageUrls) : null,
            CategoryId = dto.CategoryId,
            Condition = dto.Condition,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Не вказано" : dto.Location,
            IsShippingAvailable = dto.IsShippingAvailable,
            SellerId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Listings.Add(listing);
        await _context.SaveChangesAsync();
        return Ok(listing);
    }

    // МЕТОД ДЛЯ ВИДАЛЕННЯ ОГОЛОШЕННЯ
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteListing(int id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var listing = await _context.Listings.FindAsync(id);
        if (listing == null) return NotFound(new { error = "Оголошення не знайдено" });

        if (listing.SellerId != userId) return StatusCode(403, new { error = "Ви не можете видалити чуже оголошення" });

        _context.Listings.Remove(listing);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Оголошення успішно видалено" });
    }

    // МЕТОД ДЛЯ РЕДАГУВАННЯ ОГОЛОШЕННЯ
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateListing(int id, [FromBody] UpdateListingDto dto)
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var listing = await _context.Listings.FindAsync(id);
            if (listing == null) return NotFound(new { error = "Оголошення не знайдено" });

            if (listing.SellerId != userId) return StatusCode(403, new { error = "Ви не можете редагувати чуже оголошення" });

            listing.Title = dto.Title;
            listing.Description = dto.Description;
            listing.Price = dto.Price;
            listing.CategoryId = dto.CategoryId;
            listing.Condition = dto.Condition;
            listing.Location = string.IsNullOrWhiteSpace(dto.Location) ? "Не вказано" : dto.Location;
            listing.IsShippingAvailable = dto.IsShippingAvailable;

            if (dto.ImageUrls != null && dto.ImageUrls.Any())
            {
                listing.ImageUrl = string.Join(";", dto.ImageUrls);
            }

            await _context.SaveChangesAsync();
            return Ok(listing);
        }
        catch (Exception ex)
        {
            string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return StatusCode(500, new { error = $"Помилка БД: {realError}" });
        }
    }
    // 🔥 МЕТОД ДЛЯ ПОЛУЧЕНИЯ ОДНОГО ОБЪЯВЛЕНИЯ ПО ID (ДЛЯ СТРАНИЦЫ ДЕТАЛЕЙ)
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingById(int id)
    {
        var l = await _context.Listings
            .Include(l => l.Seller)
            .Include(l => l.Category)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (l == null)
            return NotFound(new { error = "Оголошення не знайдено" });

        var result = new
        {
            id = l.Id,
            title = l.Title,
            description = l.Description,
            price = l.Price,
            imageUrls = string.IsNullOrEmpty(l.ImageUrl) ? new List<string>() : l.ImageUrl.Split(';', StringSplitOptions.None).ToList(),
            condition = l.Condition,
            location = l.Location,
            isShippingAvailable = l.IsShippingAvailable,
            createdAt = l.CreatedAt,
            categoryName = l.Category != null ? l.Category.Name : null,
            categoryId = l.CategoryId,
            sellerId = l.SellerId,
            seller = l.Seller != null ? new { id = l.Seller.Id, firstName = l.Seller.FirstName, lastName = l.Seller.LastName, avatarUrl = l.Seller.AvatarUrl, phoneNumber = l.Seller.PhoneNumber } : null
        };

        return Ok(result);
    }
    // ДИНАМІЧНЕ ОТРИМАННЯ КАТЕГОРІЙ
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var expectedCategories = new[] {
            "📱 Електроніка та гаджети", "👗 Одяг, взуття та аксесуари",
            "📚 Навчання та книги", "🏠 Дім, сад, гуртожиток",
            "⚽ Хобі, відпочинок і спорт", "🐾 Зоотовари",
            "🚗 Транспорт та авто", "💼 Послуги та робота",
            "🎁 Віддам безкоштовно / Обмін", "📦 Інше"
        };

        bool needsSave = false;

        for (int i = 0; i < expectedCategories.Length; i++)
        {
            int catId = i + 1;
            var existing = await _context.Categories.FindAsync(catId);

            if (existing == null)
            {
                _context.Categories.Add(new Category { Id = catId, Name = expectedCategories[i] });
                needsSave = true;
            }
            else if (existing.Name != expectedCategories[i])
            {
                existing.Name = expectedCategories[i];
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await _context.SaveChangesAsync();
        }

        var categories = await _context.Categories.ToListAsync();
        return Ok(categories.Select(c => new { id = c.Id, name = c.Name }));
    }

    public class CreateListingDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string? Condition { get; set; }
        public string? Location { get; set; }
        public bool IsShippingAvailable { get; set; }
    }

    public class UpdateListingDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string? Condition { get; set; }
        public string? Location { get; set; }
        public bool IsShippingAvailable { get; set; }
    }
}