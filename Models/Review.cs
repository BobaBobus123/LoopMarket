using System.ComponentModel.DataAnnotations;

namespace TradeStorm.Api.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public int TargetUserId { get; set; } // Кому оставляют отзыв (продавец)
    public User? TargetUser { get; set; }

    [Required]
    public int AuthorId { get; set; } // Кто оставил отзыв (покупатель)
    public User? Author { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } // Оценка от 1 до 5

    public string? Comment { get; set; } // Текст отзыва

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}