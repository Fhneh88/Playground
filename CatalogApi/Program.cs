using CatalogApi.Data;
using CatalogApi.Models;
using CatalogApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=catalog.db"));
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Endpoints ────────────────────────────────────────────────────────────────

// Create product
app.MapPost("/products", async (ProductService svc, CreateProductDto dto) =>
{
    var product = await svc.CreateAsync(dto.Name, dto.Price, dto.CategoryId);
    return Results.Created($"/products/{product.Id}", product);
});

// Get all products
app.MapGet("/products", async (ProductService svc) =>
{
    var products = await svc.GetAllAsync();
    var dtos = products.Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Category?.Name ?? "")).ToList();
    return Results.Ok(dtos);
});

// Get products by category
app.MapGet("/categories/{categoryId}/products", async (ProductService svc, int categoryId) =>
{
    var products = await svc.GetByCategoryAsync(categoryId);
    var dtos = products.Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Category?.Name ?? "")).ToList();
    return Results.Ok(dtos);
});

// Update product price
app.MapPut("/products/{id}/price", async (ProductService svc, int id, UpdatePriceDto dto) =>
{
    var updated = await svc.UpdatePriceAsync(id, dto.NewPrice);
    if (!updated) return Results.NotFound();
    return Results.NoContent();
});

// Delete product
app.MapDelete("/products/{id}", async (ProductService svc, int id) =>
{
    var deleted = await svc.DeleteAsync(id);
    if (!deleted) return Results.NotFound();
    return Results.NoContent();
});

// Create category (helper endpoint)
app.MapPost("/categories", async (AppDbContext db, CreateCategoryDto dto) =>
{
    var category = new Category { Name = dto.Name };
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/categories/{category.Id}", category);
});

// Get all categories
app.MapGet("/categories", async (AppDbContext db) =>
{
    var categories = await db.Categories.AsNoTracking().ToListAsync();
    return Results.Ok(categories);
});

app.Run();

public record CreateProductDto(string Name, decimal Price, int CategoryId);
public record UpdatePriceDto(decimal NewPrice);
public record CreateCategoryDto(string Name);
public record ProductDto(int Id, string Name, decimal Price, string Category);
