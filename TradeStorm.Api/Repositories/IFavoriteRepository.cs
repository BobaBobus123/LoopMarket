using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Repositories;

public interface IFavoriteRepository : IRepository<Favorite>
{
    Task<Favorite?> GetByUserAndListingAsync(int userId, int listingId);
    Task<IReadOnlyList<object>> GetUserFavoritesAsync(int userId);
    Task<IReadOnlyList<int>> GetUserFavoriteListingIdsAsync(int userId);
}
