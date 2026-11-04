using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Repositories;

namespace TradeStorm.Api.Services;

public class ListingService : IListingService
{
    private readonly IListingRepository _listingRepository;

    public ListingService(IListingRepository listingRepository)
    {
        _listingRepository = listingRepository;
    }

    public async Task<(int TotalCount, IReadOnlyList<object> Items)> GetListingsAsync(
        string? search,
        int? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        string? condition,
        int? sellerId,
        string? status,
        string? sortBy,
        int? page,
        int? pageSize)
    {
        return await _listingRepository.GetPagedListingsAsync(
            search, categoryId, minPrice, maxPrice, condition, sellerId, status, sortBy, page, pageSize);
    }

    public async Task<Listing?> GetListingByIdAsync(int id)
    {
        return await _listingRepository.GetByIdWithDetailsAsync(id);
    }

    public async Task<Listing> CreateListingAsync(int sellerId, CreateListingDto dto)
    {
        var listing = new Listing
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            ImageUrl = dto.ImageUrls != null && dto.ImageUrls.Any() 
                ? string.Join(";", dto.ImageUrls) 
                : (dto.CategoryId == 8 || dto.Title.Contains("Послуг") ? "/images/placeholder-service.svg" : "/images/placeholder.svg"),
            CategoryId = dto.CategoryId,
            Condition = dto.Condition,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Не вказано" : dto.Location.Trim(),
            IsShippingAvailable = dto.IsShippingAvailable,
            SellerId = sellerId,
            CreatedAt = DateTime.UtcNow,
            Status = "Active"
        };

        return await _listingRepository.AddAsync(listing);
    }

    public async Task<(bool Success, string? Error, Listing? Listing)> UpdateListingAsync(int listingId, int userId, UpdateListingDto dto, bool isAdmin = false)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId);
        if (listing == null)
        {
            return (false, "Оголошення не знайдено", null);
        }

        if (listing.SellerId != userId && !isAdmin)
        {
            return (false, "Ви не маєте права редагувати чуже оголошення", null);
        }

        listing.Title = dto.Title.Trim();
        listing.Description = dto.Description.Trim();
        listing.Price = dto.Price;
        listing.CategoryId = dto.CategoryId;
        listing.Condition = dto.Condition;
        listing.Location = string.IsNullOrWhiteSpace(dto.Location) ? "Не вказано" : dto.Location.Trim();
        listing.IsShippingAvailable = dto.IsShippingAvailable;

        if (dto.ImageUrls != null)
        {
            listing.ImageUrl = dto.ImageUrls.Any() 
                ? string.Join(";", dto.ImageUrls) 
                : (listing.CategoryId == 8 || listing.Title.Contains("Послуг") ? "/images/placeholder-service.svg" : "/images/placeholder.svg");
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            listing.Status = dto.Status.Trim();
        }

        await _listingRepository.UpdateAsync(listing);
        return (true, null, listing);
    }

    public async Task<(bool Success, string? Error)> UpdateListingStatusAsync(int listingId, int userId, string status)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId);
        if (listing == null)
        {
            return (false, "Оголошення не знайдено");
        }

        if (listing.SellerId != userId)
        {
            return (false, "Ви не маєте права змінювати статус чужого оголошення");
        }

        var validStatuses = new[] { "Active", "Sold", "Reserved" };
        if (!validStatuses.Contains(status))
        {
            return (false, "Неприпустимий статус. Дозволені: Active, Sold, Reserved");
        }

        listing.Status = status;
        await _listingRepository.UpdateAsync(listing);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteListingAsync(int listingId, int userId)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId);
        if (listing == null)
        {
            return (false, "Оголошення не знайдено");
        }

        if (listing.SellerId != userId)
        {
            return (false, "Ви не можете видалити чуже оголошення");
        }

        await _listingRepository.DeleteAsync(listing);
        return (true, null);
    }
}
