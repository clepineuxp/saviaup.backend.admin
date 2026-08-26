using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdmin")]
[Route("api/admin/admin-users")]
public sealed class AdminUsersController(IAdminUsersUseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AdminIdentityDto>>> List(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminIdentityDto>> Create(CreateAdminUserRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.CreateAsync(request, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminIdentityDto>> Status(Guid id, SetStatusRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.SetStatusAsync(id, request.IsActive, cancellationToken));
}
