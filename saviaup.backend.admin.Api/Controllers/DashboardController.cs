using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminAccess")]
[Route("api/admin")]
public sealed class DashboardController(
    IAdminOverviewUseCase overview,
    IOperationStatusSettingsUseCase operationSettings) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSnapshotDto>> Dashboard(CancellationToken cancellationToken)
        => this.ToActionResult(await overview.GetDashboardAsync(cancellationToken));

    [HttpGet("operations")]
    public async Task<ActionResult<IReadOnlyCollection<OrganizationOperationDto>>> Operations(CancellationToken cancellationToken)
        => this.ToActionResult(await overview.GetOperationsAsync(cancellationToken));

    [HttpGet("operations/settings")]
    public async Task<ActionResult<OperationStatusSettingsDto>> Settings(CancellationToken cancellationToken)
        => this.ToActionResult(await operationSettings.GetAsync(cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPut("operations/settings")]
    public async Task<ActionResult<OperationStatusSettingsDto>> Settings(
        UpdateOperationStatusSettingsRequest request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await operationSettings.UpdateAsync(request, cancellationToken));
}
