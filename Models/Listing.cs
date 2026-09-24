namespace TradeStorm.Api.Models;

public class Listing
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public int SellerId { get; set; }
    public User? Seller { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}