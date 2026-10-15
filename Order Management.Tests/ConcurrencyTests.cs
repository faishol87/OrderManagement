using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Order_Management.Data;
using Order_Management.Domain;
using Order_Management.Features.Orders;

namespace Order_Management.Tests;

public class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConcurrencyTests(ApiFactory factory) => _factory = factory;

    // ============ Skenario A: concurrent stock deduction ============
    [Fact]
    public async Task A_ConcurrentStockDeduction_HanyaSatuYangBerhasil()
    {
        var productId = await _factory.SeedProductAsync("Product-A", stock: 15, price: 100m);
        var client = _factory.CreateClient();

        var body = new CreateOrderRequest(
            Guid.NewGuid(), "Jl. Test No. 1",
            new List<CreateOrderItemRequest> { new(productId, 10) });

        // tembak 2 request bersamaan
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.PostAsJsonAsync("/api/orders", body));

        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        Assert.Equal(1, created);
        Assert.Equal(1, rejected);

        // stock harus 5 (bukan minus / bukan 15)
        Assert.Equal(5, await _factory.GetStockAsync(productId));
    }

    // ============ Skenario B: concurrent status update ============
    [Fact]
    public async Task B_ConcurrentStatusUpdate_HanyaSatuYangMenang()
    {
        var productId = await _factory.SeedProductAsync("Product-B", stock: 100, price: 50m);
        var client = _factory.CreateClient();

        // buat order dulu
        var createBody = new CreateOrderRequest(
            Guid.NewGuid(), "Jl. Test No. 2",
            new List<CreateOrderItemRequest> { new(productId, 1) });
        var created = await client.PostAsJsonAsync("/api/orders", createBody);
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>();

        // dua admin update ke Confirmed bersamaan
        var patchBody = new UpdateStatusRequest(OrderStatus.Confirmed);
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.PatchAsJsonAsync($"/api/orders/{order!.Id}/status", patchBody));

        var responses = await Task.WhenAll(tasks);

        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, ok);

        // yang kalah dapat 409 (konflik concurrency) atau 422 (transisi sudah tidak valid)
        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        Assert.True(loser.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);
    }

    // ============ Skenario C: idempotent create under race ============
    [Fact]
    public async Task C_IdempotentCreateUnderRace_HanyaSatuOrder()
    {
        var productId = await _factory.SeedProductAsync("Product-C", stock: 100, price: 25m);
        var client = _factory.CreateClient();

        var idempotencyKey = Guid.NewGuid().ToString();
        var customerId = Guid.NewGuid();
        var body = new CreateOrderRequest(customerId, "Jl. Test No. 3",
            new List<CreateOrderItemRequest> { new(productId, 1) });

        Task<HttpResponseMessage> Send() 
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            return client.SendAsync(req);
        }

        // dua request dengan key sama, bersamaan
        var responses = await Task.WhenAll(Send(), Send());

        // harus tetap hanya 1 order
        Assert.Equal(1, await _factory.CountOrdersByCustomerAsync(customerId));

        // status boleh 201 (menang / mengembalikan hasil) atau 409 (sedang diproses)
        Assert.All(responses, r =>
            Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
    }
}