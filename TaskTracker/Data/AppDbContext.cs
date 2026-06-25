using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<Tag> Tags { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
            {
                e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            });

        modelBuilder.Entity<TaskItem>(e =>
            {
                e.Property(t => t.Title).IsRequired().HasMaxLength(300);
                e.HasIndex(t => t.Status);
                e.HasIndex(t => t.Priority);
            });

        modelBuilder.Entity<Tag>(e =>
            {
                e.Property(t => t.Name).IsRequired().HasMaxLength(50);
                e.HasIndex(t => t.Name).IsUnique();
            });
    }
}
