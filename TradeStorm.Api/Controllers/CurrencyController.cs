using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    /// <summary>
    /// Отримати офіційні курси валют (зовнішній REST API НБУ з кешуванням) (Етап 4)
    /// </summary>
    [HttpGet("rates")]
    public async Task<IActionResult> GetRates()
    {
        var rates = await _currencyService.GetExchangeRatesAsync();
        return Ok(rates);
    }

    /// <summary>
    /// Конвертувати ціну товару з гривні в іноземну валюту (USD, EUR, PLN) (Етап 4)
    /// </summary>
    [HttpGet("convert")]
    public async Task<IActionResult> ConvertPrice([FromQuery] decimal amount, [FromQuery] string to = "USD")
    {
        if (amount < 0) return BadRequest(new { error = "Сума не може бути від'ємною" });

        var converted = await _currencyService.ConvertPriceAsync(amount, to);
        if (converted == null)
        {
            return BadRequest(new { error = $"Невідома валюта {to}. Доступні: USD, EUR, PLN" });
        }

        return Ok(new
        {
            originalAmountUah = amount,
            targetCurrency = to.ToUpperInvariant(),
            convertedAmount = converted.Value
        });
    }
}
