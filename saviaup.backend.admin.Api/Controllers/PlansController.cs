using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminAccess")]
[Route("api/admin/plans")]
public sealed class PlansController(IPlansUseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlatformPlanDto>>> List(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.ListAsync(cancellationToken));

    [HttpGet("permissions/catalog")]
    public async Task<ActionResult<IReadOnlyCollection<PlanPermissionOptionDto>>> PermissionCatalog(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.GetPermissionCatalogAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.GetAsync(id, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<PlanDetailDto>> Create(SavePlanRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.CreateAsync(request, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlanDetailDto>> Update(Guid id, SavePlanRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PlanDetailDto>> Status(Guid id, SetPlanStatusRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.SetStatusAsync(id, request.Status, cancellationToken));
}
