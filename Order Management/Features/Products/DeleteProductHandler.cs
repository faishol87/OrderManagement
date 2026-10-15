using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Products;

public class DeleteProductHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteProductHandler> _logger;

    public DeleteProductHandler(AppDbContext db, ILogger<DeleteProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(Guid id, CancellationToken ct)
    {
        var product = await _db.Products
                          .FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw ApiException.NotFound("PRODUCT_NOT_FOUND", $"Product {id} tidak ditemukan.");

        // Jangan hapus produk yang masih dipakai order aktif
        // (OrderItem tidak punya navigasi Order, jadi pakai join)
        var inUse = await (from oi in _db.OrderItems
            join o in _db.Orders on oi.OrderId equals o.Id
            where oi.ProductId == id
                  && o.Status != OrderStatus.Delivered
                  && o.Status != OrderStatus.Cancelled
            select oi.Id).AnyAsync(ct);

        if (inUse)
            throw ApiException.Conflict("PRODUCT_IN_USE",
                "Produk masih dipakai order aktif, tidak bisa dihapus.");

        if (!product.Delete()) // sudah terhapus -> idempoten, tidak error
            return;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Product {ProductId} dihapus (soft delete)", product.Id);
    }
}