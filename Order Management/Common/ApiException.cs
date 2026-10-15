namespace Order_Management.Common;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public ApiException(int statusCode, string code, string message,
        IDictionary<string, string[]>? errors = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
    }

    public static ApiException BadRequest(string code, string message,
        IDictionary<string, string[]>? errors = null) => new(400, code, message, errors);

    public static ApiException NotFound(string code, string message) => new(404, code, message);

    public static ApiException Conflict(string code, string message) => new(409, code, message);

    public static ApiException Unprocessable(string code, string message) => new(422, code, message);
}