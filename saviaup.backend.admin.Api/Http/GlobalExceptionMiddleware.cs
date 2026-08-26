using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Api.Http;

internal sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected admin API error. CorrelationId={CorrelationId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ErrorEnvelope.From(new Error("ADMIN_INTERNAL_ERROR", "Ocurrió un error interno.", 500)), context.RequestAborted);
        }
    }
}
