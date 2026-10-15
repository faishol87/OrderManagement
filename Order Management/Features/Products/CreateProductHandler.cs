using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;

namespace Order_Management.Features.Products;

public class CreateProductHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(AppDbContext db, ILogger<CreateProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProductResponse> Handle(CreateProductRequest request, CancellationToken ct)
    {
        Validators.ValidateAndThrow(request);

        var product = Product.Create(request.Name, request.StockQuantity, request.Price);

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} dibuat", product.Id);
        return ProductMapper.ToResponse(product);
    }
}