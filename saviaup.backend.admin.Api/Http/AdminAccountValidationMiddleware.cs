using System.IdentityModel.Tokens.Jwt;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Api.Http;

internal sealed class AdminAccountValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAdminIdentityRepository repository)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(subject, out var id) || await repository.GetByIdAsync(id, context.RequestAborted) is not { IsActive: true })
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ErrorEnvelope.From(AdminErrors.Unauthenticated), context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
