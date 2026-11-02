using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly TelegramBotService _botService;

    public OrderService(AppDbContext context, IBackgroundTaskQueue taskQueue, TelegramBotService botService)
    {
        _context = context;
        _taskQueue = taskQueue;
        _botService = botService;
    }

    public async Task<OrderDto> CreateOrderAsync(int buyerId, CreateOrderDto dto)
    {
        var listing = await _context.Listings
            .Include(l => l.Seller)
            .FirstOrDefaultAsync(l => l.Id == dto.ListingId);

        if (listing == null)
            throw new KeyNotFoundException("Оголошення не знайдено");

        // Примітка: для зручності тестування та демонстрації на захисті дозволено тестове замовлення
        // if (listing.SellerId == buyerId)
        //     throw new InvalidOperationException("Ви не можете купити власний товар");

        if (listing.Status == "Sold")
            throw new InvalidOperationException("Цей товар вже продано");

        var order = new Order
        {
            ListingId = listing.Id,
            BuyerId = buyerId,
            SellerId = listing.SellerId,
            TotalPrice = listing.Price,
            Status = OrderStatus.Pending,
            BuyerName = dto.BuyerName.Trim(),
            BuyerPhone = dto.BuyerPhone.Trim(),
            DeliveryCity = dto.DeliveryCity.Trim(),
            DeliveryBranch = dto.DeliveryBranch.Trim(),
            PaymentMethod = dto.PaymentMethod ?? "Накладений платіж",
            Comment = dto.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        listing.Status = "Reserved";

        _context.Orders.Add(order);

        var notif = new Notification
        {
            UserId = listing.SellerId,
            Title = "📦 Нове замовлення!",
            Message = $"Отримано замовлення на '{listing.Title}' від {dto.BuyerName}. Сума: {listing.Price} грн.",
            Link = $"/profile.html#orders",
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        if (listing.Seller != null && listing.Seller.TelegramChatId != 0 && listing.Seller.ReceiveTelegramNotifications)
        {
            var chatId = listing.Seller.TelegramChatId;
            var text = $"🎉 <b>Нове замовлення на Loop Market!</b>\n\n" +
                       $"📦 Товар: <b>{listing.Title}</b>\n" +
                       $"💰 Сума: <b>{listing.Price} грн</b>\n" +
                       $"👤 Покупець: {dto.BuyerName} ({dto.BuyerPhone})\n" +
                       $"📍 Доставка: м. {dto.DeliveryCity}, {dto.DeliveryBranch}\n" +
                       $"💳 Оплата: {order.PaymentMethod}\n\n" +
                       $"<i>Перейдіть на сайт для підтвердження або відправки замовлення.</i>";

            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await _botService.SendNotificationAsync(chatId, text);
            });
        }

        return MapToDto(order, listing.Title, listing.ImageUrl, $"{listing.Seller?.FirstName} {listing.Seller?.LastName}".Trim(), listing.Seller?.PhoneNumber);
    }

    public async Task<List<OrderDto>> GetBuyerOrdersAsync(int buyerId)
    {
        var orders = await _context.Orders
            .Include(o => o.Listing)
            .Include(o => o.Seller)
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(o => MapToDto(
            o,
            o.Listing?.Title ?? "Товар видалено",
            o.Listing?.ImageUrl,
            $"{o.Seller?.FirstName} {o.Seller?.LastName}".Trim(),
            o.Seller?.PhoneNumber
        )).ToList();
    }

    public async Task<List<OrderDto>> GetSellerOrdersAsync(int sellerId)
    {
        var orders = await _context.Orders
            .Include(o => o.Listing)
            .Include(o => o.Buyer)
            .Where(o => o.SellerId == sellerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(o => MapToDto(
            o,
            o.Listing?.Title ?? "Товар видалено",
            o.Listing?.ImageUrl,
            $"{o.Buyer?.FirstName} {o.Buyer?.LastName}".Trim(),
            o.Buyer?.PhoneNumber
        )).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId)
    {
        var order = await _context.Orders
            .Include(o => o.Listing)
            .Include(o => o.Seller)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null) return null;

        if (order.BuyerId != userId && order.SellerId != userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Role != UserRoles.Admin)
                return null;
        }

        return MapToDto(
            order,
            order.Listing?.Title ?? "Товар видалено",
            order.Listing?.ImageUrl,
            $"{order.Seller?.FirstName} {order.Seller?.LastName}".Trim(),
            order.Seller?.PhoneNumber
        );
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, int userId, string newStatus, string? trackingNumber = null)
    {
        var order = await _context.Orders
            .Include(o => o.Listing)
            .Include(o => o.Seller)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new KeyNotFoundException("Замовлення не знайдено");

        var user = await _context.Users.FindAsync(userId);
        bool isAdmin = user?.Role == UserRoles.Admin;
        bool isSeller = order.SellerId == userId;
        bool isBuyer = order.BuyerId == userId;

        if (!isAdmin && !isSeller && !isBuyer)
            throw new UnauthorizedAccessException("Ви не маєте доступу до зміни статусу цього замовлення");

        order.Status = newStatus;

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            order.TrackingNumber = trackingNumber.Trim();
        }

        if (newStatus == OrderStatus.Completed && order.Listing != null)
        {
            order.Listing.Status = "Sold";
        }
        else if (newStatus == OrderStatus.Cancelled && order.Listing != null)
        {
            order.Listing.Status = "Active";
        }

        string notifTitle = "📦 Оновлення замовлення";
        string notifMessage = $"Статус замовлення #{order.Id} змінено на: {newStatus}.";
        if (!string.IsNullOrWhiteSpace(order.TrackingNumber))
        {
            notifMessage += $" Номер ТТН: {order.TrackingNumber}";
        }

        var notif = new Notification
        {
            UserId = isSeller ? order.BuyerId : order.SellerId,
            Title = notifTitle,
            Message = notifMessage,
            Link = $"/profile.html#orders",
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        return MapToDto(
            order,
            order.Listing?.Title ?? "Товар видалено",
            order.Listing?.ImageUrl,
            $"{order.Seller?.FirstName} {order.Seller?.LastName}".Trim(),
            order.Seller?.PhoneNumber
        );
    }

    private static OrderDto MapToDto(Order o, string listingTitle, string? listingImageUrl, string sellerName, string? sellerPhone = null)
    {
        return new OrderDto
        {
            Id = o.Id,
            ListingId = o.ListingId,
            ListingTitle = listingTitle,
            ListingImageUrl = listingImageUrl,
            TotalPrice = o.TotalPrice,
            Status = o.Status,
            BuyerName = o.BuyerName,
            BuyerPhone = o.BuyerPhone,
            DeliveryCity = o.DeliveryCity,
            DeliveryBranch = o.DeliveryBranch,
            PaymentMethod = o.PaymentMethod,
            TrackingNumber = o.TrackingNumber,
            Comment = o.Comment,
            CreatedAt = o.CreatedAt,
            BuyerId = o.BuyerId,
            SellerId = o.SellerId,
            SellerName = sellerName,
            SellerPhone = sellerPhone
        };
    }
}
