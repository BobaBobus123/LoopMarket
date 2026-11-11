using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Repositories;

public interface IListingRepository : IRepository<Listing>
{
    Task<Listing?> GetByIdWithDetailsAsync(int id);
    Task<(int TotalCount, IReadOnlyList<object> Items)> GetPagedListingsAsync(
        string? search,
        int? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        string? condition,
        int? sellerId,
        string? status,
        string? sortBy,
        int? page,
        int? pageSize,
        bool includeDrafts = false);
}
