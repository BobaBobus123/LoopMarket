using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public interface IListingService
{
    Task<(int TotalCount, IReadOnlyList<object> Items)> GetListingsAsync(
        string? search,
        int? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        string? condition,
        int? sellerId,
        string? status,
        string? sortBy,
        int? page,
        int? pageSize);

    Task<Listing?> GetListingByIdAsync(int id);
    Task<Listing> CreateListingAsync(int sellerId, CreateListingDto dto);
    Task<(bool Success, string? Error, Listing? Listing)> UpdateListingAsync(int listingId, int userId, UpdateListingDto dto, bool isAdmin = false);
    Task<(bool Success, string? Error)> UpdateListingStatusAsync(int listingId, int userId, string status);
    Task<(bool Success, string? Error)> DeleteListingAsync(int listingId, int userId);
}
