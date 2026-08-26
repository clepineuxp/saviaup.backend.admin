using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;

namespace SaviaUp.Admin.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminAccess")]
[Route("api/admin")]
public sealed class UsersController(IAdminOverviewUseCase overview, IOrganizationAdministrationUseCase administration) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformUserDto>>> Users(CancellationToken cancellationToken)
        => this.ToActionResult(await overview.GetUsersAsync(cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPost("users/{userId:guid}/password-reset")]
    public async Task<ActionResult<PasswordResetResultDto>> PasswordReset(Guid userId, CancellationToken cancellationToken)
        => this.ToActionResult(await administration.RequestPasswordResetAsync(userId, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPatch("memberships/{membershipId:guid}/status")]
    public async Task<ActionResult<PlatformUserDto>> MembershipStatus(Guid membershipId, SetStatusRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await administration.SetMembershipStatusAsync(membershipId, request.IsActive, cancellationToken));

    [Authorize(Policy = "SuperAdmin")]
    [HttpPost("memberships/reassign")]
    public async Task<ActionResult<PlatformUserDto>> Reassign(ReassignMembershipRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await administration.ReassignMembershipAsync(request, cancellationToken));
}
