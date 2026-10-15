using System.ComponentModel.DataAnnotations;
using Order_Management.Domain;

namespace Order_Management.Features.Products;

public record CreateProductRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(1, int.MaxValue)] int StockQuantity,
    [Range(0.01, double.MaxValue)] decimal Price);

public record UpdateProductRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(1, int.MaxValue)] int StockQuantity,
    [Range(0.01, double.MaxValue)] decimal Price);

public record ProductResponse(
    Guid Id, string Name, int StockQuantity, decimal Price,
    DateTime CreatedAt, DateTime? UpdatedAt);

public static class ProductMapper
{
    public static ProductResponse ToResponse(Product p)
        => new(p.Id, p.Name, p.StockQuantity, p.Price, p.CreatedAt, p.UpdatedAt);
}