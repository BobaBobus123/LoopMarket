using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Repositories;

public class ListingRepository : Repository<Listing>, IListingRepository
{
    public ListingRepository(AppDbContext context) : base(context) { }

    public async Task<Listing?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(l => l.Seller)
            .Include(l => l.Category)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<(int TotalCount, IReadOnlyList<object> Items)> GetPagedListingsAsync(
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
        bool includeDrafts = false)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(l => l.Seller)
            .Include(l => l.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(l => l.Title.ToLower().Contains(searchLower) ||
                                   (l.Description != null && l.Description.ToLower().Contains(searchLower)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(l => l.CategoryId == categoryId.Value);
        }

        if (sellerId.HasValue && sellerId.Value > 0)
        {
            query = query.Where(l => l.SellerId == sellerId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(l => l.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(l => l.Price <= maxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(condition))
        {
            query = query.Where(l => l.Condition == condition);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }
        else if (!includeDrafts)
        {
            query = query.Where(l => l.Status != "Draft");
        }

        int totalCount = await query.CountAsync();

        if (sortBy == "price_asc")
        {
            query = query.OrderByDescending(l => l.IsFeatured).ThenBy(l => l.Price);
        }
        else if (sortBy == "price_desc")
        {
            query = query.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.Price);
        }
        else
        {
            query = query.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.CreatedAt);
        }

        int p = (page.HasValue && page.Value > 0) ? page.Value : 1;
        int ps = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : 50;

        var rawItems = await query
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(l => new
            {
                id = l.Id,
                title = l.Title,
                description = l.Description,
                price = l.Price,
                imageUrl = l.ImageUrl,
                videoUrl = l.VideoUrl,
                condition = l.Condition,
                location = l.Location,
                isShippingAvailable = l.IsShippingAvailable,
                isFeatured = l.IsFeatured,
                status = l.Status,
                createdAt = l.CreatedAt,
                categoryName = l.Category != null ? l.Category.Name : "Інше",
                categoryId = l.CategoryId,
                sellerId = l.SellerId,
                viewsCount = l.ViewsCount,
                seller = l.Seller != null ? new
                {
                    id = l.Seller.Id,
                    firstName = l.Seller.FirstName,
                    lastName = l.Seller.LastName,
                    avatarUrl = l.Seller.AvatarUrl,
                    phoneNumber = l.Seller.PhoneNumber
                } : null
            })
            .ToListAsync();

        var items = rawItems.Select(l => (object)new
        {
            id = l.id,
            code = TradeStorm.Api.Services.ListingIdEncoder.Encode(l.id),
            publicId = TradeStorm.Api.Services.ListingIdEncoder.Encode(l.id),
            l.title,
            l.description,
            l.price,
            l.imageUrl,
            l.videoUrl,
            l.condition,
            l.location,
            l.isShippingAvailable,
            l.isFeatured,
            l.status,
            l.createdAt,
            l.categoryName,
            l.categoryId,
            l.sellerId,
            l.viewsCount,
            seller = l.seller != null ? new
            {
                l.seller.id,
                l.seller.firstName,
                l.seller.lastName,
                l.seller.avatarUrl,
                phoneNumber = TradeStorm.Api.Services.PhoneNumberHelper.Normalize(l.seller.phoneNumber)
            } : null
        }).ToList();

        return (totalCount, items);
    }
}
