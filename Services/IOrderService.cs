using TradeStorm.Api.DTOs;

namespace TradeStorm.Api.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(int buyerId, CreateOrderDto dto);
    Task<List<OrderDto>> GetBuyerOrdersAsync(int buyerId);
    Task<List<OrderDto>> GetSellerOrdersAsync(int sellerId);
    Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId);
    Task<OrderDto> UpdateOrderStatusAsync(int orderId, int userId, string newStatus, string? trackingNumber = null);
}
