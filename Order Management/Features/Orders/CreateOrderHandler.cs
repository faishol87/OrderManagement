using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Domain;
using Order_Management.Features.Idempotency;

namespace Order_Management.Features.Orders;

public class CreateOrderHandler
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private readonly AppDbContext _db;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(AppDbContext db, ILogger<CreateOrderHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var items = cmd.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .OrderBy(i => i.ProductId) // urutan konsisten => hindari deadlock
            .ToList();

        if (items.Count == 0)
            throw ApiException.BadRequest("EMPTY_ITEMS", "Order harus punya minimal 1 item.");

        var requestHash = ComputeHash(cmd);
        IdempotencyRecord? key = null;

        // ---- 1. Reservasi idempotency key (Skenario C) ----
        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
        {
            if (!Guid.TryParse(cmd.IdempotencyKey, out var parsedKey))
                throw ApiException.BadRequest("INVALID_IDEMPOTENCY_KEY", "Idempotency-Key harus berupa GUID.");

            key = IdempotencyRecord.Create(parsedKey, cmd.CustomerId, requestHash, IdempotencyTtl);
            _db.IdempotencyKeys.Add(key);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Kunci sudah ada -> request lain menang race
                _db.Entry(key).State = EntityState.Detached;
                var existing = await _db.IdempotencyKeys.AsNoTracking()
                    .FirstAsync(x => x.CustomerId == cmd.CustomerId && x.Key == parsedKey, ct);

                if (existing.RequestHash != requestHash)
                    throw ApiException.Unprocessable("IDEMPOTENCY_KEY_REUSED",
                        "Idempotency-Key sudah dipakai dengan payload berbeda.");

                if (existing.IsCompleted)
                {
                    _logger.LogInformation("Mengembalikan response idempoten untuk key {Key}", parsedKey);
                    return JsonSerializer.Deserialize<OrderResponse>(existing.ResponseBody!)!;
                }

                throw ApiException.Conflict("REQUEST_IN_PROGRESS",
                    "Request dengan Idempotency-Key ini sedang diproses.");
            }
        }

        // ---- 2. Proses order dalam satu transaksi ----
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var products = await _db.Products.AsNoTracking()
            .Where(p => items.Select(i => i.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Price, ct);

        if (products.Count != items.Count)
        {
            await tx.RollbackAsync(ct);
            await RemoveIdempotencyKeyAsync(key, ct);
            throw ApiException.Unprocessable("PRODUCT_NOT_FOUND", "Ada produk yang tidak ditemukan.");
        }

        foreach (var (productId, quantity) in items)
        {
            var affected = await _db.Products
                .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity - quantity), ct);

            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                await RemoveIdempotencyKeyAsync(key, ct);
                throw ApiException.Unprocessable("INSUFFICIENT_STOCK",
                    $"Stock produk {productId} tidak cukup.");
            }
        }

        var order = Order.Create(
            cmd.CustomerId,
            cmd.ShippingAddress,
            items.Select(i => (i.ProductId, i.Quantity, products[i.ProductId])));

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var response = OrderMapper.ToResponse(order);

        // ---- 3. Simpan hasil agar retry mengembalikan body yang sama ----
        if (key is not null)
        {
            key.Complete(201, JsonSerializer.Serialize(response));
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Order {OrderId} dibuat oleh customer {CustomerId}",
            order.Id, order.CustomerId);

        return response;
    }

    private async Task RemoveIdempotencyKeyAsync(IdempotencyRecord? key, CancellationToken ct)
    {
        if (key is null) return;
        var tracked = await _db.IdempotencyKeys
            .FirstOrDefaultAsync(x => x.Key == key.Key, ct);
        if (tracked is not null)
        {
            _db.IdempotencyKeys.Remove(tracked);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string ComputeHash(CreateOrderCommand cmd)
    {
        var payload = JsonSerializer.Serialize(new
        {
            cmd.CustomerId,
            cmd.ShippingAddress,
            Items = cmd.Items.OrderBy(i => i.ProductId).ToList()
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}