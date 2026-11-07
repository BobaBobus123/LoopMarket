using System.Collections.Generic;

namespace TradeStorm.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Subcategories { get; set; } = new();
}
