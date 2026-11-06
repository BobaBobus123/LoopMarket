using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly INovaPoshtaService _deliveryService;
    private readonly AppDbContext _context;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(
        INovaPoshtaService deliveryService,
        AppDbContext context,
        ILogger<DeliveryController> logger)
    {
        _deliveryService = deliveryService;
        _context = context;
        _logger = logger;
    }

    private int? TryGetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var id)) return id;
        return null;
    }

    /// <summary>
    /// Отримати перелік доступних служб доставки (Нова Пошта, Укрпошта, Самовивіз) з тарифами
    /// </summary>
    [HttpGet("carriers")]
    public IActionResult GetCarriers()
    {
        return Ok(_deliveryService.GetAvailableCarriers());
    }

    /// <summary>
    /// Пошук населених пунктів України через API Нової Пошти (з кешуванням та fallback)
    /// </summary>
    [HttpGet("novaposhta/cities")]
    public async Task<IActionResult> SearchCities([FromQuery] string query = "")
    {
        var cities = await _deliveryService.SearchCitiesAsync(query);
        return Ok(cities);
    }

    /// <summary>
    /// Отримати перелік відділень та поштоматів для обраного міста (через API Нової Пошти)
    /// </summary>
    [HttpGet("novaposhta/warehouses")]
    public async Task<IActionResult> GetWarehouses([FromQuery] string city = "Київ", [FromQuery] string? type = null)
    {
        if (string.IsNullOrWhiteSpace(city))
            city = "Київ";

        var warehouses = await _deliveryService.GetWarehousesAsync(city, type);
        return Ok(warehouses);
    }

    /// <summary>
    /// Створити електронну експрес-накладну (ТТН) Нової Пошти та прив'язати її до замовлення
    /// </summary>
    [HttpPost("novaposhta/waybill")]
    public async Task<IActionResult> CreateWaybill([FromBody] CreateWaybillDto dto)
    {
        if (dto == null)
            return BadRequest(new { error = "Некоректні дані запиту" });

        Order? order = null;
        if (dto.OrderId.HasValue && dto.OrderId.Value > 0)
        {
            order = await _context.Orders
                .Include(o => o.Listing)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId.Value);

            if (order != null)
            {
                var userId = TryGetCurrentUserId();
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Перевірка прав (тільки продавець замовлення або адмін може генерувати ТТН)
                if (userId.HasValue && userId.Value != order.SellerId && userRole != "Admin")
                {
                    return Forbid();
                }

                // Доповнюємо відсутні поля з даних замовлення
                if (string.IsNullOrWhiteSpace(dto.RecipientName))
                    dto.RecipientName = order.BuyerName;

                if (string.IsNullOrWhiteSpace(dto.RecipientPhone))
                    dto.RecipientPhone = order.BuyerPhone;

                if (string.IsNullOrWhiteSpace(dto.RecipientCity))
                    dto.RecipientCity = order.DeliveryCity;

                if (string.IsNullOrWhiteSpace(dto.RecipientWarehouse))
                    dto.RecipientWarehouse = order.DeliveryBranch;

                if (dto.Cost <= 0)
                    dto.Cost = order.TotalPrice;

                if (string.IsNullOrWhiteSpace(dto.Description) && order.Listing != null)
                    dto.Description = $"Замовлення #{order.Id}: {order.Listing.Title}";
            }
        }

        var result = await _deliveryService.CreateWaybillAsync(dto);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage ?? "Не вдалося сформувати ТТН" });
        }

        // Якщо ТТН успішно створено і є замовлення — оновлюємо його статус
        if (order != null && !string.IsNullOrWhiteSpace(result.TtnNumber))
        {
            order.TrackingNumber = result.TtnNumber;
            order.Status = OrderStatus.Shipped;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Замовлення #{OrderId} оновлено статусом Shipped з ТТН: {Ttn}", order.Id, result.TtnNumber);
        }

        return Ok(result);
    }

    /// <summary>
    /// Відстежити статус доставки ТТН Нової Пошти у реальному часі
    /// </summary>
    [HttpGet("novaposhta/track/{ttn}")]
    public async Task<IActionResult> TrackWaybill(string ttn)
    {
        var result = await _deliveryService.TrackWaybillAsync(ttn);
        if (!result.Success)
        {
            return NotFound(new { error = result.ErrorMessage ?? "Накладну не знайдено або статус недоступний" });
        }

        return Ok(result);
    }
}
