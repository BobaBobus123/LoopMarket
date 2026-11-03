using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Repositories;

public class FavoriteRepository : Repository<Favorite>, IFavoriteRepository
{
    public FavoriteRepository(AppDbContext context) : base(context) { }

    public async Task<Favorite?> GetByUserAndListingAsync(int userId, int listingId)
    {
        return await _dbSet.FirstOrDefaultAsync(f => f.UserId == userId && f.ListingId == listingId);
    }

    public async Task<IReadOnlyList<object>> GetUserFavoritesAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Include(f => f.Listing)
                .ThenInclude(l => l!.Seller)
            .Include(f => f.Listing)
                .ThenInclude(l => l!.Category)
            .Where(f => f.Listing != null)
            .Select(f => new
            {
                id = f.Listing!.Id,
                title = f.Listing.Title,
                description = f.Listing.Description,
                price = f.Listing.Price,
                imageUrl = f.Listing.ImageUrl,
                condition = f.Listing.Condition,
                location = f.Listing.Location,
                isShippingAvailable = f.Listing.IsShippingAvailable,
                status = f.Listing.Status,
                createdAt = f.Listing.CreatedAt,
                categoryName = f.Listing.Category != null ? f.Listing.Category.Name : "Інше",
                categoryId = f.Listing.CategoryId,
                sellerId = f.Listing.SellerId,
                favoritedAt = f.CreatedAt,
                seller = f.Listing.Seller != null ? new
                {
                    id = f.Listing.Seller.Id,
                    firstName = f.Listing.Seller.FirstName,
                    lastName = f.Listing.Seller.LastName,
                    avatarUrl = f.Listing.Seller.AvatarUrl,
                    phoneNumber = f.Listing.Seller.PhoneNumber
                } : null
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetUserFavoriteListingIdsAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UserId == userId)
            .Select(f => f.ListingId)
            .ToListAsync();
    }
}
