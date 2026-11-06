namespace TradeStorm.Api.DTOs;

public class DeliveryCityDto
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
}

public class DeliveryWarehouseDto
{
    public string Number { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortAddress { get; set; } = string.Empty;
    public string Type { get; set; } = "Branch"; // "Branch" or "Postomat"
    public string Ref { get; set; } = string.Empty;
}

public class DeliveryCarrierOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string EstimatedDays { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreateWaybillDto
{
    public int? OrderId { get; set; }
    public string? SenderCity { get; set; }
    public string? SenderCityRef { get; set; }
    public string? SenderWarehouseRef { get; set; }
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    
    public string RecipientCity { get; set; } = string.Empty;
    public string? RecipientCityRef { get; set; }
    public string RecipientWarehouse { get; set; } = string.Empty;
    public string? RecipientWarehouseRef { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    
    public decimal Cost { get; set; } = 300m;
    public decimal Weight { get; set; } = 1.0m;
    public string Description { get; set; } = "Товари інтернет-магазину LoopMarket";
    public string PayerType { get; set; } = "Recipient"; // "Recipient" or "Sender"
    public string PaymentMethod { get; set; } = "Cash"; // "Cash" or "NonCash"
}

public class WaybillResultDto
{
    public bool Success { get; set; }
    public string? TtnNumber { get; set; }
    public string? DocumentRef { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? EstimatedDeliveryDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TrackingResultDto
{
    public bool Success { get; set; }
    public string TtnNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusDescription { get; set; }
    public string? CitySender { get; set; }
    public string? CityRecipient { get; set; }
    public string? WarehouseRecipient { get; set; }
    public string? ScheduledDeliveryDate { get; set; }
    public string? ActualDeliveryDate { get; set; }
    public decimal? DocumentCost { get; set; }
    public string? ErrorMessage { get; set; }
}
