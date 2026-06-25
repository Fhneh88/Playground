using Microsoft.EntityFrameworkCore;
using WeatherCatalogApi;
using WeatherCatalogApi.Data;
using WeatherCatalogApi.Models;
using WeatherCatalogApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? "Data Source=catalog.db"));

builder.Services.AddHttpClient<WeatherService>();

var redisConn = builder.Configuration["Redis:ConnectionString"]!;
var redisPrefix = builder.Configuration["Redis:InstanceName"] ?? string.Empty;
builder.Services.AddCachedQueryService(redisConn, redisPrefix);

var app = builder.Build();

// Auto-create DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseHttpsRedirection();

// GET /weather/{city}
app.MapGet("/weather/{city}", async (string city, WeatherService weather) =>
{
    var result = await weather.GetWeatherAsync(city);
    return result is null
        ? Results.NotFound(new { error = $"Could not retrieve weather for '{city}'" })
        : Results.Ok(result);
});

// GET /categories
app.MapGet("/categories", async (AppDbContext db) =>
    Results.Ok(await db.Categories.OrderBy(c => c.Name).ToListAsync()));

// POST /categories
app.MapPost("/categories", async (CategoryRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Name is required" });

    var category = new Category { Name = request.Name.Trim() };
    db.Categories.Add(category);
    await db.SaveChangesAsync();

    return Results.Created($"/categories/{category.Id}", category);
});

app.Run();

record CategoryRequest(string Name);
