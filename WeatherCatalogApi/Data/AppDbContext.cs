using Microsoft.EntityFrameworkCore;
using WeatherCatalogApi.Models;

namespace WeatherCatalogApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
}
