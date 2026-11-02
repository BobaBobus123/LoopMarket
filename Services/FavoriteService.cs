using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.Models;
using TradeStorm.Api.Repositories;

namespace TradeStorm.Api.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IListingRepository _listingRepository;

    public FavoriteService(IFavoriteRepository favoriteRepository, IListingRepository listingRepository)
    {
        _favoriteRepository = favoriteRepository;
        _listingRepository = listingRepository;
    }

    public async Task<(bool IsFavorited, string Message)> ToggleFavoriteAsync(int userId, int listingId)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId);
        if (listing == null)
        {
            throw new KeyNotFoundException("Оголошення не знайдено");
        }

        var existing = await _favoriteRepository.GetByUserAndListingAsync(userId, listingId);
        if (existing != null)
        {
            await _favoriteRepository.DeleteAsync(existing);
            return (false, "Видалено з обраного");
        }

        var favorite = new Favorite
        {
            UserId = userId,
            ListingId = listingId,
            CreatedAt = DateTime.UtcNow
        };
        await _favoriteRepository.AddAsync(favorite);
        return (true, "Додано в обране");
    }

    public async Task<IReadOnlyList<object>> GetUserFavoritesAsync(int userId)
    {
        return await _favoriteRepository.GetUserFavoritesAsync(userId);
    }

    public async Task<IReadOnlyList<int>> GetUserFavoriteListingIdsAsync(int userId)
    {
        return await _favoriteRepository.GetUserFavoriteListingIdsAsync(userId);
    }
}
