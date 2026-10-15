using Microsoft.AspNetCore.Mvc;
using Order_Management.Common;
using Order_Management.Domain;
using Order_Management.Logging;

namespace Order_Management.Features.Orders;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly CreateOrderHandler _create;
    private readonly UpdateStatusHandler _updateStatus;
    private readonly CancelOrderHandler _cancel;
    private readonly ListOrderHandler _list;
    private readonly GetOrderHandler _getOrder;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        CreateOrderHandler create,
        UpdateStatusHandler updateStatus,
        CancelOrderHandler cancel,
        ListOrderHandler list,
        GetOrderHandler getOrder,
        ILogger<OrderController> logger)
    {
        _create = create;
        _updateStatus = updateStatus;
        _cancel = cancel;
        _list = list;
        _getOrder = getOrder;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        var cmd = new CreateOrderCommand(
            request.CustomerId,
            request.ShippingAddress,
            request.Items.Select(i => (i.ProductId, i.Quantity)).ToList(),
            idempotencyKey);

        var result = await _create.Handle(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _getOrder.Handle(id, ct);

        if (order is null)
            throw ApiException.NotFound("ORDER_NOT_FOUND", $"Order {id} tidak ditemukan.");

        return Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _list.Handle(status, customerId, from, to, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        Validators.ValidateAndThrow(request);
        var result = await _updateStatus.Handle(id, request.Status, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _cancel.Handle(id, ct);
        return Ok(result);
    }
}