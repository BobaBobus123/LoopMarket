using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListingsController : ControllerBase
{
    private readonly IListingService _listingService;
    private readonly ICategoryService _categoryService;
    private readonly IWebHostEnvironment _env;

    public ListingsController(
        IListingService listingService,
        ICategoryService categoryService,
        IWebHostEnvironment env)
    {
        _listingService = listingService;
        _categoryService = categoryService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetListings(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? condition,
        [FromQuery] int? sellerId,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var (totalCount, listings) = await _listingService.GetListingsAsync(
            search, categoryId, minPrice, maxPrice, condition, sellerId, status, sortBy, page, pageSize);

        return Ok(new { totalCount, listings });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingById(int id)
    {
        var l = await _listingService.GetListingByIdAsync(id);
        if (l == null)
        {
            return NotFound(new { error = "Оголошення не знайдено" });
        }

        var result = new
        {
            id = l.Id,
            title = l.Title,
            description = l.Description,
            price = l.Price,
            imageUrls = string.IsNullOrEmpty(l.ImageUrl)
                ? new List<string>()
                : l.ImageUrl.Split(';', StringSplitOptions.None).ToList(),
            condition = l.Condition,
            location = l.Location,
            isShippingAvailable = l.IsShippingAvailable,
            status = l.Status ?? "Active",
            createdAt = l.CreatedAt,
            categoryName = l.Category != null ? l.Category.Name : null,
            categoryId = l.CategoryId,
            sellerId = l.SellerId,
            seller = l.Seller != null ? new
            {
                id = l.Seller.Id,
                firstName = l.Seller.FirstName,
                lastName = l.Seller.LastName,
                avatarUrl = l.Seller.AvatarUrl,
                phoneNumber = l.Seller.PhoneNumber
            } : null
        };

        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized(new { error = "Невірний токен авторизації" });
        }

        var listing = await _listingService.CreateListingAsync(userId, dto);
        return CreatedAtAction(nameof(GetListingById), new { id = listing.Id }, listing);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateListing(int id, [FromBody] UpdateListingDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var (success, error, listing) = await _listingService.UpdateListingAsync(id, userId, dto);
        if (!success)
        {
            if (error == "Оголошення не знайдено")
                return NotFound(new { error });

            return StatusCode(403, new { error });
        }

        return Ok(listing);
    }

    [HttpPatch("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateListingStatus(int id, [FromBody] UpdateStatusDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var (success, error) = await _listingService.UpdateListingStatusAsync(id, userId, dto.Status);
        if (!success)
        {
            if (error == "Оголошення не знайдено")
                return NotFound(new { error });

            return StatusCode(403, new { error });
        }

        return Ok(new { id, status = dto.Status });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteListing(int id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var (success, error) = await _listingService.DeleteListingAsync(id, userId);
        if (!success)
        {
            if (error == "Оголошення не знайдено")
                return NotFound(new { error });

            return StatusCode(403, new { error });
        }

        return Ok(new { message = "Оголошення успішно видалено" });
    }

    /// <summary>
    /// Адміністрування: модераційне видалення будь-якого оголошення (Етап 6 RBAC)
    /// </summary>
    [HttpDelete("admin/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteListing(int id)
    {
        var listing = await _listingService.GetListingByIdAsync(id);
        if (listing == null)
        {
            return NotFound(new { error = "Оголошення не знайдено" });
        }

        await _listingService.DeleteListingAsync(id, listing.SellerId);
        return Ok(new { message = "Оголошення успішно видалено адміністратором платформи" });
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        var result = categories.Select(c => new { id = c.Id, name = c.Name });
        return Ok(result);
    }

    [HttpPost("upload-multiple")]
    [Authorize]
    public async Task<IActionResult> UploadMultipleImages([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { error = "Файли не обрано" });
        }

        string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string uploadsFolder = Path.Combine(webRootPath, "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var uploadedUrls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    continue; // Пропускаємо недопустимі формати
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    continue; // Пропускаємо файли більше 5 МБ
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "_")}{extension}";
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
}

public class UpdateStatusDto
{
    public string Status { get; set; } = "Active";
}
