using System.IdentityModel.Tokens.Jwt;
using SaviaUp.Admin.Application.Ports;

namespace SaviaUp.Admin.Api.Http;

internal sealed class AdminActorContext(IHttpContextAccessor accessor) : IAdminActorContext
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? accessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? accessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? string.Empty;

    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}
