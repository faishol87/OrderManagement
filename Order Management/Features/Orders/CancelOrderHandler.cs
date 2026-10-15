using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Orders;

public class CancelOrderHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CancelOrderHandler> _logger;

    public CancelOrderHandler(AppDbContext db, ILogger<CancelOrderHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(Guid orderId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw ApiException.NotFound("ORDER_NOT_FOUND", $"Order {orderId} tidak ditemukan.");

        if (!order.IsCancellable)
            throw ApiException.Unprocessable("ORDER_NOT_CANCELLABLE",
                $"Order dengan status {order.Status} tidak bisa dibatalkan.");

        order.TryChangeStatus(OrderStatus.Cancelled);

        try
        {
            // Guard atomik: hanya berhasil kalau RowVersion masih sama
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw ApiException.Conflict("ORDER_MODIFIED",
                "Order sudah diubah oleh proses lain. Silakan muat ulang.");
        }

        // Kembalikan stock (atomik). Karena satu transaksi, kalau gagal semua dibatalkan.
        foreach (var item in order.Items.OrderBy(i => i.ProductId))
        {
            await _db.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity + item.Quantity), ct);
        }

        await tx.CommitAsync(ct);

        _logger.LogInformation("Order {OrderId} dibatalkan", order.Id);
        return OrderMapper.ToResponse(order);
    }
}