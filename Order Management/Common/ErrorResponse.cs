namespace Order_Management.Common;

public record ErrorResponse(
    string TraceId,
    string Code,
    string Message,
    IDictionary<string, string[]>? Errors = null);