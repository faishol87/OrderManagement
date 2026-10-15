using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;

namespace Order_Management.Features.Products;

public class GetProductHandler
{
    private readonly AppDbContext _db;
    public GetProductHandler(AppDbContext db) => _db = db;

    public async Task<ProductResponse> Handle(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking()
                          .FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw ApiException.NotFound("PRODUCT_NOT_FOUND", $"Product {id} tidak ditemukan.");

        return ProductMapper.ToResponse(product);
    }
}