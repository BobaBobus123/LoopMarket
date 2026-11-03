using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradeStorm.Api.Services;

public record CurrencyRate(string CurrencyCode, string CurrencyName, decimal Rate, System.DateTime UpdatedAt);

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyRate>> GetExchangeRatesAsync();
    Task<decimal?> ConvertPriceAsync(decimal amountUah, string targetCurrency);
}
