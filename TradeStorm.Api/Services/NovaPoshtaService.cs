using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeStorm.Api.DTOs;

namespace TradeStorm.Api.Services;

public class NovaPoshtaService : INovaPoshtaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NovaPoshtaService> _logger;
    private readonly IConfiguration _config;

    private const string ApiUrl = "https://api.novaposhta.ua/v2.0/json/";

    public NovaPoshtaService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<NovaPoshtaService> logger,
        IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _config = config;
    }

    private string GetApiKey()
    {
        var key = _config["NovaPoshta:ApiKey"];
        return !string.IsNullOrWhiteSpace(key) ? key : "bf8900a77c105022ff2ff848cc2a5225";
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
                apiKey = GetApiKey(),
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
                            var present = addr.TryGetProperty("Present", out var pr) ? pr.GetString() ?? "" : "";
                            var mainDesc = addr.TryGetProperty("MainDescription", out var md) ? md.GetString() ?? "" : "";
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
                if (type.Equals("Postomat", StringComparison.OrdinalIgnoreCase))
                {
                    props["TypeOfWarehouseRef"] = "f9316480-5f2d-425d-bc2c-ac7cd29decf0";
                }
            }

            var requestBody = new
            {
                apiKey = GetApiKey(),
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
                        var whRef = item.TryGetProperty("Ref", out var rf) ? rf.GetString() ?? "" : "";
                        var isPostomat = desc.Contains("Поштомат", StringComparison.OrdinalIgnoreCase) ||
                                         desc.Contains("поштомат", StringComparison.OrdinalIgnoreCase);

                        result.Add(new DeliveryWarehouseDto
                        {
                            Number = number,
                            Description = desc,
                            ShortAddress = shortAddr,
                            Ref = whRef,
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

    public async Task<WaybillResultDto> CreateWaybillAsync(CreateWaybillDto dto)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NovaPoshtaClient");
            var apiKey = GetApiKey();

            // 1. Налаштування відправника
            var senderRef = _config["NovaPoshta:SenderRef"] ?? "72d54d56-a7db-11ed-a60f-48df37b921db";
            var senderContactRef = _config["NovaPoshta:SenderContactRef"] ?? "72d762b8-a7db-11ed-a60f-48df37b921db";
            var senderPhone = _config["NovaPoshta:SenderPhone"] ?? "380636686292";
            var senderCityRef = !string.IsNullOrWhiteSpace(dto.SenderCityRef) 
                ? dto.SenderCityRef 
                : (_config["NovaPoshta:SenderCityRef"] ?? "8d5a9813-391c-11dd-90d9-001a92567626"); // Кременчук
            var senderWarehouseRef = !string.IsNullOrWhiteSpace(dto.SenderWarehouseRef)
                ? dto.SenderWarehouseRef
                : (_config["NovaPoshta:SenderWarehouseRef"] ?? "1ec09d3e-e1c2-11e3-8c4a-0050568002cf"); // Відділення №1

            // 2. Визначення міста отримувача (CityRef)
            var recipientCityRef = dto.RecipientCityRef;
            if (string.IsNullOrWhiteSpace(recipientCityRef) || !Guid.TryParse(recipientCityRef, out _))
            {
                var searchCities = await SearchCitiesAsync(dto.RecipientCity, 1);
                recipientCityRef = searchCities.FirstOrDefault()?.Ref ?? "8d5a980d-391c-11dd-90d9-001a92567626"; // Fallback: Київ
            }

            // 3. Визначення відділення отримувача (WarehouseRef)
            var recipientWarehouseRef = dto.RecipientWarehouseRef;
            if (string.IsNullOrWhiteSpace(recipientWarehouseRef) || !Guid.TryParse(recipientWarehouseRef, out _))
            {
                var warehouses = await GetWarehousesAsync(recipientCityRef, limit: 100);
                var matchedWh = warehouses.FirstOrDefault(w => 
                    (!string.IsNullOrWhiteSpace(dto.RecipientWarehouse) && 
                     (w.Number == dto.RecipientWarehouse || w.Description.Contains(dto.RecipientWarehouse))));

                recipientWarehouseRef = matchedWh?.Ref ?? warehouses.FirstOrDefault()?.Ref;
                if (string.IsNullOrWhiteSpace(recipientWarehouseRef))
                {
                    recipientWarehouseRef = "1ec09d3e-e1c2-11e3-8c4a-0050568002cf";
                }
            }

            // 4. Очищення та форматування телефону отримувача
            var rawPhone = (dto.RecipientPhone ?? string.Empty).Trim()
                .Replace("+", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "");
            if (rawPhone.StartsWith("0") && rawPhone.Length == 10)
            {
                rawPhone = "38" + rawPhone;
            }
            if (string.IsNullOrWhiteSpace(rawPhone) || rawPhone.Length < 10)
            {
                rawPhone = "380991112233";
            }

            // 5. Розбір ПІБ отримувача
            var nameParts = (dto.RecipientName ?? "Покупець LoopMarket").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lastName = nameParts.Length > 0 ? nameParts[0] : "Користувач";
            var firstName = nameParts.Length > 1 ? nameParts[1] : "Отримувач";
            var middleName = nameParts.Length > 2 ? nameParts[2] : "";

            // 6. Створення контрагента-отримувача в системі Нової Пошти (Counterparty.save)
            string recipientRef = string.Empty;
            string contactRecipientRef = string.Empty;

            var cpBody = new
            {
                apiKey = apiKey,
                modelName = "Counterparty",
                calledMethod = "save",
                methodProperties = new
                {
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    Phone = rawPhone,
                    Email = "",
                    CounterpartyType = "PrivatePerson",
                    CounterpartyProperty = "Recipient"
                }
            };

            var cpResponse = await client.PostAsync(ApiUrl, new StringContent(JsonSerializer.Serialize(cpBody), Encoding.UTF8, "application/json"));
            if (cpResponse.IsSuccessStatusCode)
            {
                var cpContent = await cpResponse.Content.ReadAsStringAsync();
                using var cpDoc = JsonDocument.Parse(cpContent);
                if (cpDoc.RootElement.TryGetProperty("data", out var cpData) && cpData.GetArrayLength() > 0)
                {
                    var cpItem = cpData[0];
                    recipientRef = cpItem.TryGetProperty("Ref", out var rRef) ? rRef.GetString() ?? "" : "";
                    if (cpItem.TryGetProperty("ContactPerson", out var contactElem) && 
                        contactElem.TryGetProperty("data", out var cData) && 
                        cData.GetArrayLength() > 0)
                    {
                        contactRecipientRef = cData[0].TryGetProperty("Ref", out var cRef) ? cRef.GetString() ?? "" : "";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(recipientRef))
            {
                recipientRef = "72d5cf3d-a7db-11ed-a60f-48df37b921db";
            }
            if (string.IsNullOrWhiteSpace(contactRecipientRef))
            {
                contactRecipientRef = "01a1b305-0c4e-7067-8c26-1bfb10057a49";
            }

            // 7. Створення експрес-накладної ТТН (InternetDocument.save)
            var docProps = new Dictionary<string, object>
            {
                { "SenderWarehouseIndex", "" },
                { "RecipientWarehouseIndex", "" },
                { "PayerType", string.IsNullOrWhiteSpace(dto.PayerType) ? "Recipient" : dto.PayerType },
                { "PaymentMethod", string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod },
                { "DateTime", DateTime.UtcNow.ToString("dd.MM.yyyy") },
                { "CargoType", "Parcel" },
                { "VolumeGeneral", "0.005" },
                { "Weight", (dto.Weight > 0 ? dto.Weight : 1.0m).ToString("0.0", CultureInfo.InvariantCulture) },
                { "ServiceType", "WarehouseWarehouse" },
                { "SeatsAmount", "1" },
                { "Description", string.IsNullOrWhiteSpace(dto.Description) ? "Товари інтернет-магазину LoopMarket" : dto.Description },
                { "Cost", ((int)Math.Max(dto.Cost, 100m)).ToString() },
                { "CitySender", senderCityRef },
                { "Sender", senderRef },
                { "SenderAddress", senderWarehouseRef },
                { "ContactSender", senderContactRef },
                { "SendersPhone", senderPhone },
                { "CityRecipient", recipientCityRef },
                { "Recipient", recipientRef },
                { "RecipientAddress", recipientWarehouseRef },
                { "ContactRecipient", contactRecipientRef },
                { "RecipientsPhone", rawPhone }
            };

            var docBody = new
            {
                apiKey = apiKey,
                modelName = "InternetDocument",
                calledMethod = "save",
                methodProperties = docProps
            };

            var docResponse = await client.PostAsync(ApiUrl, new StringContent(JsonSerializer.Serialize(docBody), Encoding.UTF8, "application/json"));
            if (docResponse.IsSuccessStatusCode)
            {
                var docContent = await docResponse.Content.ReadAsStringAsync();
                using var resDoc = JsonDocument.Parse(docContent);

                var success = resDoc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                if (success && resDoc.RootElement.TryGetProperty("data", out var ttnData) && ttnData.GetArrayLength() > 0)
                {
                    var firstTtn = ttnData[0];
                    var ttnNumber = firstTtn.TryGetProperty("IntDocNumber", out var num) ? num.GetString() : null;
                    var docRef = firstTtn.TryGetProperty("Ref", out var r) ? r.GetString() : null;
                    var cost = firstTtn.TryGetProperty("CostOnSite", out var c) ? (decimal?)c.GetDecimal() : null;
                    var estDate = firstTtn.TryGetProperty("EstimatedDeliveryDate", out var ed) ? ed.GetString() : null;

                    _logger.LogInformation("Успішно створено ТТН Нової Пошти: {Ttn}", ttnNumber);

                    return new WaybillResultDto
                    {
                        Success = true,
                        TtnNumber = ttnNumber,
                        DocumentRef = docRef,
                        EstimatedCost = cost,
                        EstimatedDeliveryDate = estDate
                    };
                }
                else
                {
                    var errors = new List<string>();
                    if (resDoc.RootElement.TryGetProperty("errors", out var errElem))
                    {
                        foreach (var err in errElem.EnumerateArray())
                        {
                            errors.Add(err.GetString() ?? "");
                        }
                    }
                    var errorMsg = string.Join(", ", errors);
                    _logger.LogWarning("Помилка при збереженні ТТН Нової Пошти: {Error}", errorMsg);
                    return new WaybillResultDto
                    {
                        Success = false,
                        ErrorMessage = !string.IsNullOrWhiteSpace(errorMsg) ? errorMsg : "Помилка формування накладної"
                    };
                }
            }

            return new WaybillResultDto
            {
                Success = false,
                ErrorMessage = $"Помилка сервера Нової Пошти (HTTP {(int)docResponse.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критична помилка при створенні ТТН Нової Пошти");
            return new WaybillResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TrackingResultDto> TrackWaybillAsync(string ttn)
    {
        ttn = (ttn ?? string.Empty).Trim().Replace(" ", "");
        if (string.IsNullOrWhiteSpace(ttn))
        {
            return new TrackingResultDto
            {
                Success = false,
                ErrorMessage = "Не вказано номер експрес-накладної"
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("NovaPoshtaClient");
            var apiKey = GetApiKey();

            var reqBody = new
            {
                apiKey = apiKey,
                modelName = "TrackingDocument",
                calledMethod = "getStatusDocuments",
                methodProperties = new
                {
                    Documents = new[]
                    {
                        new { DocumentNumber = ttn, Phone = "" }
                    }
                }
            };

            var response = await client.PostAsync(ApiUrl, new StringContent(JsonSerializer.Serialize(reqBody), Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                if (success && doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                {
                    var item = dataElem[0];
                    var status = item.TryGetProperty("Status", out var st) ? st.GetString() ?? "" : "";
                    var statusCode = item.TryGetProperty("StatusCode", out var sc) ? sc.GetString() ?? "" : "";
                    var citySender = item.TryGetProperty("CitySender", out var cs) ? cs.GetString() : null;
                    var cityRecipient = item.TryGetProperty("CityRecipient", out var cr) ? cr.GetString() : null;
                    var whRecipient = item.TryGetProperty("WarehouseRecipient", out var wr) ? wr.GetString() : null;
                    var schedDate = item.TryGetProperty("ScheduledDeliveryDate", out var sd) ? sd.GetString() : null;
                    var actDate = item.TryGetProperty("ActualDeliveryDate", out var ad) ? ad.GetString() : null;
                    decimal? docCost = null;
                    if (item.TryGetProperty("DocumentCost", out var dc) && dc.ValueKind == JsonValueKind.Number)
                    {
                        docCost = dc.GetDecimal();
                    }

                    return new TrackingResultDto
                    {
                        Success = true,
                        TtnNumber = ttn,
                        Status = status,
                        StatusCode = statusCode,
                        StatusDescription = status,
                        CitySender = citySender,
                        CityRecipient = cityRecipient,
                        WarehouseRecipient = whRecipient,
                        ScheduledDeliveryDate = schedDate,
                        ActualDeliveryDate = actDate,
                        DocumentCost = docCost
                    };
                }
            }

            return new TrackingResultDto
            {
                Success = false,
                TtnNumber = ttn,
                ErrorMessage = "Не вдалося отримати статус накладної з сервісу Нової Пошти"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при відстеженні ТТН Нової Пошти: {Ttn}", ttn);
            return new TrackingResultDto
            {
                Success = false,
                TtnNumber = ttn,
                ErrorMessage = ex.Message
            };
        }
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
            new DeliveryWarehouseDto { Number = "1", Description = "Відділення №1: вул. Флотська, 2 (Вантажне до 1100 кг)", ShortAddress = "вул. Флотська, 2", Type = "Branch", Ref = "1ec09d3e-e1c2-11e3-8c4a-0050568002cf" },
            new DeliveryWarehouseDto { Number = "2", Description = "Відділення №2: вул. Небесної Сотні, 57 (до 30 кг)", ShortAddress = "вул. Небесної Сотні, 57", Type = "Branch", Ref = "1ec0a130-e1c2-11e3-8c4a-0050568002cf" },
            new DeliveryWarehouseDto { Number = "3", Description = "Відділення №3: просп. Лесі Українки, 138 (до 30 кг)", ShortAddress = "просп. Лесі Українки, 138", Type = "Branch", Ref = "1ec0a4b4-e1c2-11e3-8c4a-0050568002cf" },
            new DeliveryWarehouseDto { Number = "4", Description = "Відділення №4: вул. Соборна, 28/40 (до 30 кг)", ShortAddress = "вул. Соборна, 28/40", Type = "Branch", Ref = "1ec0a7ac-e1c2-11e3-8c4a-0050568002cf" },
            new DeliveryWarehouseDto { Number = "5", Description = "Відділення №5: вул. Першотравнева, 20 (до 30 кг)", ShortAddress = "вул. Першотравнева, 20", Type = "Branch", Ref = "1ec0aa9a-e1c2-11e3-8c4a-0050568002cf" },
            new DeliveryWarehouseDto { Number = "2210", Description = "Поштомат №2210: вул. Соборна, 15 (ТЦ «Промінь»)", ShortAddress = "вул. Соборна, 15", Type = "Postomat", Ref = "3887c2b3-53d9-11ec-897b-b49691ee8909" },
            new DeliveryWarehouseDto { Number = "3105", Description = "Поштомат №3105: просп. Свободи, 45 (Супермаркет «АТБ»)", ShortAddress = "просп. Свободи, 45", Type = "Postomat", Ref = "040ad085-3e28-11ec-897b-b49691ee8909" },
            new DeliveryWarehouseDto { Number = "4401", Description = "Поштомат №4401: вул. Шевченка, 22/30", ShortAddress = "вул. Шевченка, 22/30", Type = "Postomat", Ref = "0359aa75-0e9e-11ed-a60f-48df37b921db" }
        };
    }
}
