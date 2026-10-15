using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Orders;

public class GetOrderHandler
{
    private readonly AppDbContext _db;

    public GetOrderHandler(AppDbContext db) => _db = db;

    public async Task<OrderResponse?> Handle(
        Guid? orderId,
        CancellationToken ct)
    {
        var query = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        
        return query is not null ? OrderMapper.ToResponse(query) :  null;
    }
}