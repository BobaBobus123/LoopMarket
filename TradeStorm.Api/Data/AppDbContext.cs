using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Listing> Listings { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Унікальний індекс для обраного: користувач не може двічі додати один товар в обране
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.ListingId })
            .IsUnique();

        // Індекси для прискорення фільтрації та пошуку (Етап 2 та 4)
        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.CategoryId);

        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.Price);

        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.CreatedAt);

        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.Status);

        // Налаштування зв'язків замовлень
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Buyer)
            .WithMany()
            .HasForeignKey(o => o.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Seller)
            .WithMany()
            .HasForeignKey(o => o.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Listing)
            .WithMany()
            .HasForeignKey(o => o.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.BuyerId);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.SellerId);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status);
    }
}
