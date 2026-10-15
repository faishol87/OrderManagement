using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order_Management.Data;
using Order_Management.Domain;
using Testcontainers.MsSql;

namespace Order_Management.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private string ConnectionString { get; set; } = default!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // pakai database khusus test
        ConnectionString = new SqlConnectionStringBuilder(_db.GetConnectionString())
        {
            InitialCatalog = "OrderTests"
        }.ConnectionString;

        // trigger host dibangun, lalu jalankan migration
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // buang DbContext lama, ganti ke connection string container
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(ConnectionString));
        });
    }

    // helper seeding & query
    public async Task<Guid> SeedProductAsync(string name, int stock, decimal price)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = Product.Create(name, stock, price);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public async Task<int> GetStockAsync(Guid productId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.IgnoreQueryFilters()
            .Where(p => p.Id == productId)
            .Select(p => p.StockQuantity)
            .SingleAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
    
    public async Task<int> CountOrdersByCustomerAsync(Guid customerId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.CountAsync(o => o.CustomerId == customerId);
    }
}