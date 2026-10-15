using Microsoft.EntityFrameworkCore;
using Order_Management.Domain;

namespace Order_Management.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        
        if (await db.Products.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("Seeder dilewati, data produk sudah ada.");
            return;
        }

        var products = new[]
        {
            Product.Create("Keyboard Mechanical", stockQuantity: 50, price: 750_000m),
            Product.Create("Mouse Wireless",      stockQuantity: 100, price: 250_000m),
            Product.Create("Monitor 24 inch",     stockQuantity: 20, price: 2_000_000m),
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Berhasil seed {Count} produk.", products.Length);
    }
}