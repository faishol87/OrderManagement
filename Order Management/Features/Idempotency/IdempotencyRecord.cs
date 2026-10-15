namespace Order_Management.Features.Idempotency;

public class IdempotencyRecord
{
    public Guid Key { get; private set; }
    public Guid CustomerId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public int? StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(Guid key, Guid customerId, string requestHash, TimeSpan ttl)
    {
        var now = DateTime.UtcNow;
        return new IdempotencyRecord
        {
            Key = key,
            CustomerId = customerId,
            RequestHash = requestHash,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl)
        };
    }

    public void Complete(int statusCode, string responseBody)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public bool IsCompleted => StatusCode.HasValue;
}