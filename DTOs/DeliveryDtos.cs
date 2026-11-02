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
