using CatalogApi.Data;
using CatalogApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogApi.Services;

public class ProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db) => _db = db;

    public async Task<Product> CreateAsync(string name, decimal price, int categoryId)
    {
        var product = new Product { Name = name, Price = price, CategoryId = categoryId };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .ToListAsync();
    }

    public async Task<List<Product>> GetByCategoryAsync(int categoryId)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .Include(p => p.Category)
            .ToListAsync();
    }

    public async Task<bool> UpdatePriceAsync(int productId, decimal newPrice)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return false;

        product.Price = newPrice;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return false;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }
}
