using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminAccess")]
[Route("api/admin/organizations")]
public sealed class OrganizationsController(
    IAdminOverviewUseCase overview,
    IOrganizationAdministrationUseCase administration,
    IPlansUseCase plans) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OrganizationSummaryDto>>> List(CancellationToken cancellationToken)
        => this.ToActionResult(await overview.GetOrganizationsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => this.ToActionResult(await overview.GetOrganizationAsync(id, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OrganizationSummaryDto>> Status(Guid id, SetStatusRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await administration.SetStatusAsync(id, request.IsActive, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPatch("{id:guid}/permissions/{permissionCode}")]
    public async Task<ActionResult<OrganizationDetailDto>> Permission(Guid id, string permissionCode, SetPermissionRequest request, CancellationToken cancellationToken)
    {
        var changed = await plans.SetPermissionOverrideAsync(id, permissionCode, request.Enabled, cancellationToken);
        return !changed.IsSuccess ? this.Error(changed.Error!) : this.ToActionResult(await overview.GetOrganizationAsync(id, cancellationToken));
    }

    [Authorize(Policy = "SuperAdmin")]
    [HttpPut("{id:guid}/owner")]
    public async Task<ActionResult<OrganizationDetailDto>> Owner(Guid id, ChangeOwnerRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await administration.ChangeOwnerAsync(id, request.UserId, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPut("{id:guid}/plan")]
    public async Task<ActionResult<PlanAssignmentResultDto>> AssignPlan(Guid id, AssignPlanRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await plans.AssignAsync(id, request, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPost("{id:guid}/permissions/sync")]
    public async Task<ActionResult<PlanAssignmentResultDto>> RetrySync(Guid id, CancellationToken cancellationToken)
        => this.ToActionResult(await plans.RetrySyncAsync(id, cancellationToken));
}
