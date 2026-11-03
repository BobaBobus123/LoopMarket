using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public int TargetUserId { get; set; } // Кому залишають відгук (продавець)
    public User? TargetUser { get; set; }

    [Required]
    public int AuthorId { get; set; } // Хто залишив відгук (покупець)
    public User? Author { get; set; }

    public int? ListingId { get; set; } // Оголошення, до якого прив'язаний відгук
    public Listing? Listing { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } // Оцінка від 1 до 5

    public string? Comment { get; set; } // Текст відгуку

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
