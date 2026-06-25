using Microsoft.EntityFrameworkCore;
using OrdersLinqDemo.Models;

namespace OrdersLinqDemo.Data;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=orders.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(100);
            entity.Property(o => o.City).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Total).HasColumnType("decimal(18,2)");
        });
    }
}
