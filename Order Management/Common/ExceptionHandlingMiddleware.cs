using Order_Management.Common;

namespace Order_Management.Common;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "ApiException {Code}: {Message}", ex.Code, ex.Message);
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, 500, "INTERNAL_ERROR",
                "Terjadi kesalahan pada server.", null);
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string code,
        string message, IDictionary<string, string[]>? errors)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var body = new ErrorResponse(ctx.TraceIdentifier, code, message, errors);
        await ctx.Response.WriteAsJsonAsync(body);
    }
}