using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;

namespace TradeStorm.Api.Services;

public interface INovaPoshtaService
{
    Task<List<DeliveryCityDto>> SearchCitiesAsync(string query, int limit = 10);
    Task<List<DeliveryWarehouseDto>> GetWarehousesAsync(string cityRefOrName, string? type = null, int limit = 80);
    List<DeliveryCarrierOptionDto> GetAvailableCarriers();
}
