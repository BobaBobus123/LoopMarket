using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    /// <summary>
    /// Отримати всі збережені оголошення поточного користувача
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized(new { error = "Невірний токен авторизації" });
        }

        var favorites = await _favoriteService.GetUserFavoritesAsync(userId);
        return Ok(favorites);
    }

    /// <summary>
    /// Отримати масив ID оголошень в обраному (для синхронізації UI сердечок)
    /// </summary>
    [HttpGet("ids")]
    public async Task<IActionResult> GetFavoriteIds()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized(new { error = "Невірний токен авторизації" });
        }

        var ids = await _favoriteService.GetUserFavoriteListingIdsAsync(userId);
        return Ok(ids);
    }

    /// <summary>
    /// Перемкнути статус обраного (додати/видалити)
    /// </summary>
    [HttpPost("toggle/{listingId}")]
    [HttpPost("{listingId}")]
    public async Task<IActionResult> ToggleFavorite(string listingId)
    {
        if (!ListingIdEncoder.TryDecode(listingId, out int numericId))
        {
            return NotFound(new { error = "Оголошення не знайдено" });
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized(new { error = "Невірний токен авторизації" });
        }

        try
        {
            var result = await _favoriteService.ToggleFavoriteAsync(userId, numericId);
            return Ok(new { isFavorited = result.IsFavorited, message = result.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Видалити оголошення з обраного
    /// </summary>
    [HttpDelete("{listingId}")]
    public async Task<IActionResult> RemoveFavorite(int listingId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return Unauthorized(new { error = "Невірний токен авторизації" });
        }

        try
        {
            var result = await _favoriteService.ToggleFavoriteAsync(userId, listingId);
            return Ok(new { isFavorited = result.IsFavorited, message = result.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
