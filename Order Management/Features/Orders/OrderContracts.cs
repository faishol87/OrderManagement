using System.ComponentModel.DataAnnotations;
using Order_Management.Domain;

namespace Order_Management.Features.Orders;

public record CreateOrderItemRequest(
    [Required] Guid ProductId,
    [Range(1, int.MaxValue)] int Quantity);

public record CreateOrderRequest(
    [Required] Guid CustomerId,
    [Required, StringLength(500, MinimumLength = 3)] string ShippingAddress,
    [Required, MinLength(1)] List<CreateOrderItemRequest> Items);

public record UpdateStatusRequest([Required] OrderStatus Status);

public record CreateOrderCommand(
    Guid CustomerId,
    string ShippingAddress,
    IReadOnlyList<(Guid ProductId, int Quantity)> Items,
    string? IdempotencyKey);

public record OrderItemResponse(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderResponse(
    Guid Id, Guid CustomerId, string Status, string ShippingAddress,
    DateTime CreatedAt, DateTime UpdatedAt, decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class OrderMapper
{
    public static OrderResponse ToResponse(Order o)
    {
        var items = o.Items
            .Select(i => new OrderItemResponse(i.ProductId, i.Quantity, i.UnitPrice))
            .ToList();

        return new OrderResponse(
            o.Id, o.CustomerId, o.Status.ToString(), o.ShippingAddress,
            o.CreatedAt, o.UpdatedAt ?? o.CreatedAt,
            items.Sum(i => i.Quantity * i.UnitPrice), items);
    }
}