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

        if (listing.SellerId == buyerId)
            throw new InvalidOperationException("Ви не можете купити власний товар");

        if (listing.Status == "Sold")
            throw new InvalidOperationException("Цей товар вже продано");

        var isService = listing.CategoryId == 8 || listing.Title.Contains("Послуг", StringComparison.OrdinalIgnoreCase);

        decimal totalPrice = (dto.CustomTotalPrice.HasValue && dto.CustomTotalPrice.Value > 0)
            ? dto.CustomTotalPrice.Value
            : listing.Price;

        // Генерація захищеного непослідовного коду замовлення
        var orderCode = GenerateOrderCode();
        var order = new Order
        {
            OrderCode = orderCode,
            ListingId = listing.Id,
            BuyerId = buyerId,
            SellerId = listing.SellerId,
            TotalPrice = totalPrice,
            Status = OrderStatus.Pending,
            BuyerName = dto.BuyerName.Trim(),
            BuyerPhone = dto.BuyerPhone.Trim(),
            DeliveryCity = dto.DeliveryCity.Trim(),
            DeliveryBranch = dto.DeliveryBranch.Trim(),
            PaymentMethod = dto.PaymentMethod ?? "Накладений платіж",
            Comment = dto.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        if (!isService)
        {
            listing.Status = "Reserved";
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(); // Зберігаємо спочатку для отримання дійсного ID замовлення

        var displayCode = order.GetDisplayCode();

        var notifTitle = isService ? $"🛠 Замовлення {displayCode}" : $"📦 Замовлення {displayCode}";
        var notifMessage = isService
            ? $"Послуга «{listing.Title}» • {totalPrice:N0} ₴ • Замовник: {dto.BuyerName}"
            : $"Товар «{listing.Title}» • {totalPrice:N0} ₴ • Покупець: {dto.BuyerName} ({dto.DeliveryCity})";

        var notif = new Notification
        {
            UserId = listing.SellerId,
            Title = notifTitle,
            Message = notifMessage,
            Link = $"/profile.html#orders",
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);

        var listingPhoto = !string.IsNullOrWhiteSpace(listing.ImageUrl)
            ? listing.ImageUrl.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
            : null;
        var photoLine = !string.IsNullOrWhiteSpace(listingPhoto) ? $"• Фото: {listingPhoto.Trim()}\n" : "";

        // Структуроване, красиве та інформативне повідомлення в чат між покупцем і продавцем
        var chatMsgText = isService
            ? $"🛠 ЗАМОВЛЕННЯ ПОСЛУГИ {displayCode}\n\n" +
              $"• Послуга: {listing.Title}\n" +
              $"• Вартість: {totalPrice:N0} ₴\n" +
              photoLine +
              $"• Замовник: {dto.BuyerName}\n" +
              $"• Локація: {dto.DeliveryCity}\n" +
              $"• Деталі: {dto.DeliveryBranch}\n" +
              $"• Оплата: {order.PaymentMethod}" +
              (!string.IsNullOrWhiteSpace(dto.Comment) ? $"\n• Коментар: {dto.Comment}" : "")
            : $"📦 ЗАМОВЛЕННЯ ТОВАРУ {displayCode}\n\n" +
              $"• Товар: {listing.Title}\n" +
              $"• Вартість: {totalPrice:N0} ₴\n" +
              photoLine +
              $"• Покупець: {dto.BuyerName}\n" +
              $"• Доставка: {dto.DeliveryCity}\n" +
              $"• Відділення: {dto.DeliveryBranch}\n" +
              $"• Оплата: {order.PaymentMethod}" +
              (!string.IsNullOrWhiteSpace(dto.Comment) ? $"\n• Коментар: {dto.Comment}" : "");

        var chatMsg = new Message
        {
            SenderId = buyerId,
            ReceiverId = listing.SellerId,
            Text = chatMsgText,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Messages.Add(chatMsg);

        await _context.SaveChangesAsync();
        if (listing.Seller != null && listing.Seller.TelegramChatId != 0 && listing.Seller.ReceiveTelegramNotifications)
        {
            var chatId = listing.Seller.TelegramChatId;
            string text;
            if (isService)
            {
                text = $"🛠 <b>Нове замовлення послуги на Loop Market!</b>\n━━━━━━━━━━━━━━━━━━\n" + $"🆔 Код замовлення: <code>{displayCode}</code>\n" +
                       $"🛠 Послуга: <b>{System.Net.WebUtility.HtmlEncode(listing.Title)}</b>\n" +
                       $"💰 Сума: <b>{totalPrice:N0} грн</b>\n" +
                       $"👤 Замовник: {System.Net.WebUtility.HtmlEncode(dto.BuyerName)} ({System.Net.WebUtility.HtmlEncode(dto.BuyerPhone)})\n" +
                       $"📍 Адреса/Локація: {System.Net.WebUtility.HtmlEncode(dto.DeliveryCity)}\n" +
                       $"⏱ Деталі замовлення: {System.Net.WebUtility.HtmlEncode(dto.DeliveryBranch)}\n" +
                       $"💳 Оплата: {System.Net.WebUtility.HtmlEncode(order.PaymentMethod)}\n" +
                       (!string.IsNullOrWhiteSpace(order.Comment) ? $"💬 Коментар: <i>{System.Net.WebUtility.HtmlEncode(order.Comment)}</i>\n\n" : "\n") +
                       $"<i>Перейдіть на сайт для підтвердження або зв'язку з клієнтом.</i>";
            }
            else
            {
                text = $"🎉 <b>Нове замовлення на Loop Market!</b>\n━━━━━━━━━━━━━━━━━━\n" + $"🆔 Код замовлення: <code>{displayCode}</code>\n" +
                       $"📦 Товар: <b>{System.Net.WebUtility.HtmlEncode(listing.Title)}</b>\n" +
                       $"💰 Сума: <b>{totalPrice:N0} грн</b>\n" +
                       $"👤 Покупець: {System.Net.WebUtility.HtmlEncode(dto.BuyerName)} ({System.Net.WebUtility.HtmlEncode(dto.BuyerPhone)})\n" +
                       $"📍 Доставка: {System.Net.WebUtility.HtmlEncode(dto.DeliveryCity)}, {System.Net.WebUtility.HtmlEncode(dto.DeliveryBranch)}\n" +
                       $"💳 Оплата: {System.Net.WebUtility.HtmlEncode(order.PaymentMethod)}\n" +
                       (!string.IsNullOrWhiteSpace(order.Comment) ? $"💬 Коментар: <i>{System.Net.WebUtility.HtmlEncode(order.Comment)}</i>\n\n" : "\n") +
                       $"<i>Перейдіть на сайт для підтвердження або відправки замовлення.</i>";
            }

            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await _botService.SendNotificationAsync(chatId, text);
            });
        }

        return MapToDto(order, listing.Title, listingPhoto, $"{listing.Seller?.FirstName} {listing.Seller?.LastName}".Trim(), listing.Seller?.PhoneNumber, isService);
    }

    public async Task<List<OrderDto>> GetBuyerOrdersAsync(int buyerId)
    {
        var orders = await _context.Orders
            .Include(o => o.Listing)
            .Include(o => o.Seller)
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(o => MapToDto(o)).ToList();
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
            o.Buyer?.PhoneNumber,
            o.Listing?.CategoryId == 8 || (o.Listing?.Title ?? "").Contains("Послуг", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(o.DeliveryBranch) && o.DeliveryBranch.Contains("год"))
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

        return MapToDto(order);
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

        var dispCode = order.GetDisplayCode();
        string ukrStatus = newStatus switch
        {
            OrderStatus.Pending => "Очікує підтвердження",
            OrderStatus.Confirmed => "Підтверджено продавцем",
            OrderStatus.Shipped => "Відправлено",
            OrderStatus.Completed => "Успішно завершено",
            OrderStatus.Cancelled => "Скасовано",
            _ => newStatus
        };

        string notifTitle = $"📦 Оновлення замовлення {dispCode}";
        string notifMessage = $"Статус замовлення {dispCode} змінено на: {ukrStatus}.";
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

        if (order.Buyer != null && order.Buyer.TelegramChatId != 0 && order.Buyer.ReceiveTelegramNotifications)
        {
            var buyerText = $"📦 <b>Оновлення вашого замовлення {dispCode}!</b>\n" +
                            $"━━━━━━━━━━━━━━━━━━\n" +
                            $"📦 Товар: <b>{System.Net.WebUtility.HtmlEncode(order.Listing?.Title ?? "Товар")}</b>\n" +
                            $"⚡ Новий статус: <b>{ukrStatus}</b>\n" +
                            (!string.IsNullOrWhiteSpace(order.TrackingNumber) ? $"🚚 ТТН Нової Пошти: <code>{order.TrackingNumber}</code>\n" : "") +
                            $"━━━━━━━━━━━━━━━━━━\n" +
                            $"<i>Дякуємо, що обираєте Loop Market!</i>";

            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await _botService.SendNotificationAsync(order.Buyer.TelegramChatId, buyerText);
            });
        }
        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order o) => MapToDto(
        o,
        o.Listing?.Title ?? "Товар видалено",
        o.Listing?.ImageUrl,
        $"{o.Seller?.FirstName} {o.Seller?.LastName}".Trim(),
        o.Seller?.PhoneNumber,
        o.Listing?.CategoryId == 8 || (o.Listing?.Title ?? "").Contains("Послуг", StringComparison.OrdinalIgnoreCase) || (o.DeliveryBranch != null && o.DeliveryBranch.Contains("год"))
    );

    private static string GenerateOrderCode()
    {
        var code = Random.Shared.Next(100000, 999999);
        return $"LP-{code}";
    }

    private static OrderDto MapToDto(Order o, string listingTitle, string? listingImageUrl, string sellerName, string? sellerPhone = null, bool isService = false)
    {
        return new OrderDto
        {
            Id = o.Id,
            OrderCode = o.GetDisplayCode(),
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
            SellerPhone = sellerPhone,
            IsService = isService
        };
    }

    public async Task<OrderDto> UpdateOrderPriceAsync(int orderId, int userId, decimal newPrice)
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

        if (!isAdmin && !isSeller)
            throw new UnauthorizedAccessException("Тільки продавець або адміністратор може змінювати суму замовлення");

        if (newPrice < 0)
            throw new InvalidOperationException("Сума не може бути від'ємною");

        order.TotalPrice = newPrice;
        await _context.SaveChangesAsync();

        return MapToDto(order);
    }
}