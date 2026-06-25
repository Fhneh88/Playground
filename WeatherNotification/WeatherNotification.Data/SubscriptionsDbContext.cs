using Microsoft.EntityFrameworkCore;
using WeatherNotification.Data.Models;

namespace WeatherNotification.Data;

public class SubscriptionsDbContext : DbContext
{
    public SubscriptionsDbContext(DbContextOptions<SubscriptionsDbContext> options) : base(options) { }

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.City).IsRequired().HasMaxLength(100);
            e.Property(s => s.Email).IsRequired().HasMaxLength(200);
        });
    }
}
