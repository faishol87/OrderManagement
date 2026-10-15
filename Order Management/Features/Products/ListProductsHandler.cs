using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Features.Orders;

namespace Order_Management.Features.Products;

public class ListProductsHandler
{
    private readonly AppDbContext _db;
    public ListProductsHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<ProductResponse>> Handle(
        string? name, int page, int pageSize, CancellationToken ct)
    {
        Validators.EnsurePagination(page, pageSize);

        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(p => p.Name.Contains(name));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductResponse(
                p.Id, p.Name, p.StockQuantity, p.Price, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items, page, pageSize, total);
    }
}