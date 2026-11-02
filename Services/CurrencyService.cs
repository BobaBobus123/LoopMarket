using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace TradeStorm.Api.Services;

public class CurrencyService : ICurrencyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrencyService> _logger;
    private const string CacheKey = "ExchangeRates_NBU";

    public CurrencyService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<CurrencyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CurrencyRate>> GetExchangeRatesAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<CurrencyRate>? cachedRates) && cachedRates != null)
        {
            return cachedRates;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("NbuApiClient");
            var response = await client.GetAsync("https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?json");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var nbuItems = JsonSerializer.Deserialize<List<NbuExchangeItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (nbuItems != null)
                {
                    var rates = nbuItems
                        .Where(i => i.Cc == "USD" || i.Cc == "EUR" || i.Cc == "PLN" || i.Cc == "GBP")
                        .Select(i => new CurrencyRate(i.Cc, i.Txt, i.Rate, DateTime.UtcNow))
                        .ToList();

                    // Кешування на 60 хвилин для усунення надлишкових зовнішніх запитів (Етап 4)
                    _cache.Set(CacheKey, rates, TimeSpan.FromMinutes(60));
                    return rates;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Збій виклику зовнішнього сервісу НБУ: {Message}. Застосовано резервний кеш.", ex.Message);
        }

        // Резервний fallback курс у разі недоступності зовнішньої мережі
        var fallbackRates = new List<CurrencyRate>
        {
            new("USD", "Долар США", 41.50m, DateTime.UtcNow),
            new("EUR", "Євро", 44.80m, DateTime.UtcNow),
            new("PLN", "Польський злотий", 10.45m, DateTime.UtcNow)
        };
        return fallbackRates;
    }

    public async Task<decimal?> ConvertPriceAsync(decimal amountUah, string targetCurrency)
    {
        if (targetCurrency.Equals("UAH", StringComparison.OrdinalIgnoreCase))
        {
            return amountUah;
        }

        var rates = await GetExchangeRatesAsync();
        var rateItem = rates.FirstOrDefault(r => r.CurrencyCode.Equals(targetCurrency, StringComparison.OrdinalIgnoreCase));
        if (rateItem == null || rateItem.Rate == 0)
        {
            return null;
        }

        return Math.Round(amountUah / rateItem.Rate, 2);
    }

    private class NbuExchangeItem
    {
        public string Txt { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Cc { get; set; } = string.Empty;
    }
}
