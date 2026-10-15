using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Orders;

public class UpdateStatusHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateStatusHandler> _logger;

    public UpdateStatusHandler(AppDbContext db, ILogger<UpdateStatusHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(Guid orderId, OrderStatus newStatus, CancellationToken ct)
    {
        if (newStatus == OrderStatus.Cancelled)
            throw ApiException.Unprocessable("USE_CANCEL_ENDPOINT",
                "Untuk membatalkan order, gunakan endpoint POST /api/orders/{id}/cancel.");

        var order = await _db.Orders
                        .Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw ApiException.NotFound("ORDER_NOT_FOUND", $"Order {orderId} tidak ditemukan.");

        if (!order.TryChangeStatus(newStatus))
            throw ApiException.Unprocessable("INVALID_STATUS_TRANSITION",
                $"Transisi dari {order.Status} ke {newStatus} tidak diizinkan.");

        try
        {
            await _db.SaveChangesAsync(ct); // WHERE RowVersion = ... (Skenario B)
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ApiException.Conflict("ORDER_MODIFIED",
                "Order sudah diubah oleh proses lain. Silakan muat ulang.");
        }

        _logger.LogInformation("Order {OrderId} status -> {Status}", order.Id, newStatus);
        return OrderMapper.ToResponse(order);
    }
}