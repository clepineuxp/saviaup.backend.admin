using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AuthController(IAdminAuthUseCase useCase) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("admin-auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AdminSessionDto>> Login(LoginRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.LoginAsync(request, cancellationToken));
}
