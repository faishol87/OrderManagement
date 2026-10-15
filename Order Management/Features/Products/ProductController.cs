using Microsoft.AspNetCore.Mvc;
using Order_Management.Common;
using Order_Management.Features.Orders;

namespace Order_Management.Features.Products;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly CreateProductHandler _create;
    private readonly GetProductHandler _get;
    private readonly ListProductsHandler _list;
    private readonly DeleteProductHandler _delete;

    public ProductController(
        CreateProductHandler create,
        GetProductHandler get,
        ListProductsHandler list,
        DeleteProductHandler delete)
    {
        _create = create;
        _get = get;
        _list = list;
        _delete = delete;
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await _create.Handle(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _get.Handle(id, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] string? name,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _list.Handle(name, page, pageSize, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _delete.Handle(id, ct);
        return NoContent();
    }
}