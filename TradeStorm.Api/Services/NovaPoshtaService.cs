using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TradeStorm.Api.DTOs;

namespace TradeStorm.Api.Services;

public class NovaPoshtaService : INovaPoshtaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NovaPoshtaService> _logger;

    private const string ApiUrl = "https://api.novaposhta.ua/v2.0/json/";

    public NovaPoshtaService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<NovaPoshtaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public List<DeliveryCarrierOptionDto> GetAvailableCarriers()
    {
        return new List<DeliveryCarrierOptionDto>
        {
            new DeliveryCarrierOptionDto
            {
                Id = "novaposhta_branch",
                Name = "Нова Пошта (Відділення)",
                Icon = "box",
                EstimatedDays = "1-2 дні",
                EstimatedCost = 80m,
                Description = "Доставка у будь-яке з 15 000+ відділень по Україні з можливістю огляду товару"
            },
            new DeliveryCarrierOptionDto
            {
                Id = "novaposhta_postomat",
                Name = "Нова Пошта (Поштомат)",
                Icon = "postomat",
                EstimatedDays = "1-2 дні",
                EstimatedCost = 70m,
                Description = "Цілодобове безконтактне отримання біля вашого будинку чи гуртожитку"
            },
            new DeliveryCarrierOptionDto
            {
                Id = "ukrposhta_standard",
                Name = "Укрпошта (Експрес / Стандарт)",
                Icon = "mail",
                EstimatedDays = "2-4 дні",
                EstimatedCost = 45m,
                Description = "Економічна доставка у будь-який населений пункт та поштове відділення України"
            },
            new DeliveryCarrierOptionDto
            {
                Id = "pickup",
                Name = "Самовивіз / Особиста зустріч",
                Icon = "user-check",
                EstimatedDays = "Сьогодні",
                EstimatedCost = 0m,
                Description = "Безкоштовно. Зустріч з продавцем за вказаною локацією для огляду та оплати"
            }
        };
    }

    public async Task<List<DeliveryCityDto>> SearchCitiesAsync(string query, int limit = 10)
    {
        query = (query ?? string.Empty).Trim();
        var cacheKey = $"np_cities_{query.ToLowerInvariant()}_{limit}";

        if (_cache.TryGetValue(cacheKey, out List<DeliveryCityDto>? cached) && cached != null)
        {
            return cached;
        }

        var result = new List<DeliveryCityDto>();

        try
        {
            var client = _httpClientFactory.CreateClient("NovaPoshtaClient");

            var requestBody = new
            {
                apiKey = "",
                modelName = "Address",
                calledMethod = "searchSettlements",
                methodProperties = new
                {
                    CityName = query,
                    Limit = limit.ToString(),
                    Page = "1"
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                {
                    var firstItem = dataElem[0];
                    if (firstItem.TryGetProperty("Addresses", out var addressesElem))
                    {
                        foreach (var addr in addressesElem.EnumerateArray())
                        {
                            var present = addr.GetProperty("Present").GetString() ?? "";
                            var mainDesc = addr.GetProperty("MainDescription").GetString() ?? "";
                            var deliveryCity = addr.TryGetProperty("DeliveryCity", out var dc) ? dc.GetString() ?? "" : "";
                            var area = addr.TryGetProperty("Area", out var ar) ? ar.GetString() ?? "" : "";

                            result.Add(new DeliveryCityDto
                            {
                                Name = mainDesc,
                                FullName = present,
                                Ref = deliveryCity,
                                Area = area
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка при виклику Nova Poshta API для міст. Використання резервного списку.");
        }

        // Резервний список популярних міст України (fallback)
        if (!result.Any())
        {
            result = GetFallbackCities(query);
        }

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(60));
        return result;
    }

    public async Task<List<DeliveryWarehouseDto>> GetWarehousesAsync(string cityRefOrName, string? type = null, int limit = 80)
    {
        cityRefOrName = (cityRefOrName ?? string.Empty).Trim();
        var cacheKey = $"np_wh_{cityRefOrName.ToLowerInvariant()}_{type}_{limit}";

        if (_cache.TryGetValue(cacheKey, out List<DeliveryWarehouseDto>? cached) && cached != null)
        {
            return cached;
        }

        var result = new List<DeliveryWarehouseDto>();

        try
        {
            var client = _httpClientFactory.CreateClient("NovaPoshtaClient");

            var props = new Dictionary<string, string>
            {
                ["Limit"] = limit.ToString(),
                ["Page"] = "1"
            };

            // Якщо передано GUID міста (CityRef)
            if (Guid.TryParse(cityRefOrName, out _))
            {
                props["CityRef"] = cityRefOrName;
            }
            else
            {
                props["CityName"] = cityRefOrName;
            }

            if (!string.IsNullOrEmpty(type))
            {
                // Поштомати або відділення
                if (type.Equals("Postomat", StringComparison.OrdinalIgnoreCase))
                {
                    props["TypeOfWarehouseRef"] = "f9316480-5f2d-425d-bc2c-ac7cd29decf0"; // Postomat ref
                }
            }

            var requestBody = new
            {
                apiKey = "",
                modelName = "AddressGeneral",
                calledMethod = "getWarehouses",
                methodProperties = props
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    foreach (var item in dataElem.EnumerateArray())
                    {
                        var desc = item.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "";
                        var number = item.TryGetProperty("Number", out var num) ? num.GetString() ?? "" : "";
                        var shortAddr = item.TryGetProperty("ShortAddress", out var sa) ? sa.GetString() ?? "" : "";
                        var isPostomat = desc.Contains("Поштомат", StringComparison.OrdinalIgnoreCase) ||
                                         desc.Contains("поштомат", StringComparison.OrdinalIgnoreCase);

                        result.Add(new DeliveryWarehouseDto
                        {
                            Number = number,
                            Description = desc,
                            ShortAddress = shortAddr,
                            Type = isPostomat ? "Postomat" : "Branch"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка при отриманні відділень Нової Пошти. Використання резервного списку.");
        }

        if (!result.Any())
        {
            result = GetFallbackWarehouses(cityRefOrName);
        }

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(60));
        return result;
    }

    private List<DeliveryCityDto> GetFallbackCities(string query)
    {
        var all = new List<DeliveryCityDto>
        {
            new DeliveryCityDto { Name = "Кременчук", FullName = "м. Кременчук, Полтавська обл.", Ref = "8d5a9813-391c-11dd-90d9-001a92567626", Area = "Полтавська" },
            new DeliveryCityDto { Name = "Київ", FullName = "м. Київ, Київська обл.", Ref = "8d5a980d-391c-11dd-90d9-001a92567626", Area = "Київська" },
            new DeliveryCityDto { Name = "Полтава", FullName = "м. Полтава, Полтавська обл.", Ref = "db5c8892-391c-11dd-90d9-001a92567626", Area = "Полтавська" },
            new DeliveryCityDto { Name = "Харків", FullName = "м. Харків, Харківська обл.", Ref = "db5c88e0-391c-11dd-90d9-001a92567626", Area = "Харківська" },
            new DeliveryCityDto { Name = "Дніпро", FullName = "м. Дніпро, Дніпропетровська обл.", Ref = "db5c88f0-391c-11dd-90d9-001a92567626", Area = "Дніпропетровська" },
            new DeliveryCityDto { Name = "Одеса", FullName = "м. Одеса, Одеська обл.", Ref = "db5c88d0-391c-11dd-90d9-001a92567626", Area = "Одеська" },
            new DeliveryCityDto { Name = "Львів", FullName = "м. Львів, Львівська обл.", Ref = "db5c88f5-391c-11dd-90d9-001a92567626", Area = "Львівська" },
            new DeliveryCityDto { Name = "Запоріжжя", FullName = "м. Запоріжжя, Запорізька обл.", Ref = "db5c88c0-391c-11dd-90d9-001a92567626", Area = "Запорізька" },
            new DeliveryCityDto { Name = "Вінниця", FullName = "м. Вінниця, Вінницька обл.", Ref = "db5c88b0-391c-11dd-90d9-001a92567626", Area = "Вінницька" },
            new DeliveryCityDto { Name = "Черкаси", FullName = "м. Черкаси, Черкаська обл.", Ref = "db5c88a0-391c-11dd-90d9-001a92567626", Area = "Черкаська" }
        };

        if (string.IsNullOrWhiteSpace(query)) return all;
        return all.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                              c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private List<DeliveryWarehouseDto> GetFallbackWarehouses(string cityName)
    {
        return new List<DeliveryWarehouseDto>
        {
            new DeliveryWarehouseDto { Number = "1", Description = "Відділення №1: вул. Флотська, 2 (Вантажне до 1100 кг)", ShortAddress = "вул. Флотська, 2", Type = "Branch" },
            new DeliveryWarehouseDto { Number = "2", Description = "Відділення №2: вул. Небесної Сотні, 57 (до 30 кг)", ShortAddress = "вул. Небесної Сотні, 57", Type = "Branch" },
            new DeliveryWarehouseDto { Number = "3", Description = "Відділення №3: просп. Лесі Українки, 138 (до 30 кг)", ShortAddress = "просп. Лесі Українки, 138", Type = "Branch" },
            new DeliveryWarehouseDto { Number = "4", Description = "Відділення №4: вул. Соборна, 28/40 (до 30 кг)", ShortAddress = "вул. Соборна, 28/40", Type = "Branch" },
            new DeliveryWarehouseDto { Number = "5", Description = "Відділення №5: вул. Першотравнева, 20 (до 30 кг)", ShortAddress = "вул. Першотравнева, 20", Type = "Branch" },
            new DeliveryWarehouseDto { Number = "2210", Description = "Поштомат №2210: вул. Соборна, 15 (ТЦ «Промінь»)", ShortAddress = "вул. Соборна, 15", Type = "Postomat" },
            new DeliveryWarehouseDto { Number = "3105", Description = "Поштомат №3105: просп. Свободи, 45 (Супермаркет «АТБ»)", ShortAddress = "просп. Свободи, 45", Type = "Postomat" },
            new DeliveryWarehouseDto { Number = "4401", Description = "Поштомат №4401: вул. Шевченка, 22/30", ShortAddress = "вул. Шевченка, 22/30", Type = "Postomat" }
        };
    }
}
