using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Orders;

public class ListOrderHandler
{
    private readonly AppDbContext _db;

    public ListOrderHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<OrderResponse>> Handle(
        OrderStatus? status,
        Guid? customerId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        Validators.EnsurePagination(page, pageSize);

        var query = _db.Orders.AsNoTracking().AsQueryable();

        if (status.HasValue)     query = query.Where(o => o.Status == status.Value);
        if (customerId.HasValue) query = query.Where(o => o.CustomerId == customerId.Value);
        if (from.HasValue)       query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue)         query = query.Where(o => o.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = orders.Select(OrderMapper.ToResponse).ToList();
        return new PagedResult<OrderResponse>(items, page, pageSize, total);
    }
}