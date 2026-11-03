using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradeStorm.Api.Services;

public interface IFavoriteService
{
    Task<(bool IsFavorited, string Message)> ToggleFavoriteAsync(int userId, int listingId);
    Task<IReadOnlyList<object>> GetUserFavoritesAsync(int userId);
    Task<IReadOnlyList<int>> GetUserFavoriteListingIdsAsync(int userId);
}
