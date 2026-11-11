using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var id)) return id;
        throw new UnauthorizedAccessException("Користувач не авторизований");
    }

    [HttpPost("create-service")]
    public async Task<ActionResult<OrderDto>> CreateServiceOrder([FromBody] CreateOrderDto dto)
    {
        if (string.IsNullOrEmpty(dto.DeliveryCity))
            dto.DeliveryCity = dto.Address ?? "м. Київ";
        if (string.IsNullOrEmpty(dto.DeliveryBranch))
            dto.DeliveryBranch = !string.IsNullOrEmpty(dto.ExecutionDate)
                ? $"За адресою {dto.Address ?? "замовника"}, {dto.ExecutionDate} {dto.ExecutionTime}".Trim()
                : "За адресою замовника";
        if (!dto.CustomTotalPrice.HasValue && dto.TotalPrice.HasValue)
            dto.CustomTotalPrice = dto.TotalPrice;
        if (string.IsNullOrEmpty(dto.Comment) && !string.IsNullOrEmpty(dto.Specifications))
            dto.Comment = dto.Specifications;

        return await CreateOrder(dto);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto dto)
    {
        try
        {
            if (string.IsNullOrEmpty(dto.DeliveryCity)) dto.DeliveryCity = dto.Address ?? "м. Київ";
            if (string.IsNullOrEmpty(dto.DeliveryBranch)) dto.DeliveryBranch = "Відділення / Адреса";
            var userId = GetCurrentUserId();
            var order = await _orderService.CreateOrderAsync(userId, dto);
            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-purchases")]
    public async Task<ActionResult<List<OrderDto>>> GetMyPurchases()
    {
        var userId = GetCurrentUserId();
        var orders = await _orderService.GetBuyerOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("my-sales")]
    public async Task<ActionResult<List<OrderDto>>> GetMySales()
    {
        var userId = GetCurrentUserId();
        var orders = await _orderService.GetSellerOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var userId = GetCurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(id, userId);
        if (order == null) return NotFound(new { error = "Замовлення не знайдено або доступ заборонено" });
        return Ok(order);
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var order = await _orderService.UpdateOrderStatusAsync(id, userId, dto.Status, dto.TrackingNumber);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/price")]
    public async Task<ActionResult<OrderDto>> UpdatePrice(int id, [FromBody] UpdateOrderPriceDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var order = await _orderService.UpdateOrderPriceAsync(id, userId, dto.TotalPrice);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Адміністрування: перегляд усіх угод платформи для оператора
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminGetAllOrders([FromServices] AppDbContext db)
    {
        var rawOrders = await db.Orders
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .Include(o => o.Listing)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                id = o.Id,
                listingId = o.ListingId,
                listingTitle = o.Listing != null ? o.Listing.Title : "Товар / послуга",
                listingImageUrl = o.Listing != null ? o.Listing.ImageUrl : null,
                buyerId = o.BuyerId,
                buyerName = o.Buyer != null ? $"{o.Buyer.FirstName} {o.Buyer.LastName}".Trim() : "Покупець",
                buyerPhone = o.Buyer != null ? o.Buyer.PhoneNumber : string.Empty,
                sellerId = o.SellerId,
                sellerName = o.Seller != null ? $"{o.Seller.FirstName} {o.Seller.LastName}".Trim() : "Продавець",
                sellerPhone = o.Seller != null ? o.Seller.PhoneNumber : string.Empty,
                status = o.Status,
                totalPrice = o.TotalPrice,
                trackingNumber = o.TrackingNumber,
                createdAt = o.CreatedAt
            })
            .ToListAsync();

        var orders = rawOrders.Select(o => new
        {
            o.id,
            o.listingId,
            o.listingTitle,
            o.listingImageUrl,
            o.buyerId,
            o.buyerName,
            buyerPhone = TradeStorm.Api.Services.PhoneNumberHelper.Normalize(o.buyerPhone) ?? string.Empty,
            o.sellerId,
            o.sellerName,
            sellerPhone = TradeStorm.Api.Services.PhoneNumberHelper.Normalize(o.sellerPhone) ?? string.Empty,
            o.status,
            o.totalPrice,
            o.trackingNumber,
            o.createdAt
        }).ToList();

        return Ok(orders);
    }
}
