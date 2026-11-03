using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly INovaPoshtaService _deliveryService;

    public DeliveryController(INovaPoshtaService deliveryService)
    {
        _deliveryService = deliveryService;
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
    public async Task<IActionResult> GetWarehouses([FromQuery] string city, [FromQuery] string? type = null)
    {
        if (string.IsNullOrWhiteSpace(city))
            return BadRequest(new { error = "Параметр 'city' (назва або код міста) обов'язковий" });

        var warehouses = await _deliveryService.GetWarehousesAsync(city, type);
        return Ok(warehouses);
    }
}
