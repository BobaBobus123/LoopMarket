using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Listing> Listings { get; set; }
}