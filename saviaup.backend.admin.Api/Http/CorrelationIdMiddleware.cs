namespace SaviaUp.Admin.Api.Http;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await next(context);
    }

    private static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 120 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}
