using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto dto)
    {
        try
        {
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
}