using System;

namespace TradeStorm.Api.Models;

public class Favorite
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
