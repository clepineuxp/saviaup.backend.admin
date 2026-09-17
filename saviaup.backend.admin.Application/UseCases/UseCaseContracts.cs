using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Application.UseCases;

public interface IAdminAuthUseCase
{
    Task<Result<AdminSessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface IAdminUsersUseCase
{
    Task<Result<IReadOnlyCollection<AdminIdentityDto>>> ListAsync(CancellationToken cancellationToken);
    Task<Result<AdminIdentityDto>> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken);
    Task<Result<AdminIdentityDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}

public interface IPlansUseCase
{
    Task<Result<IReadOnlyCollection<PlatformPlanDto>>> ListAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<PlanPermissionOptionDto>>> GetPermissionCatalogAsync(CancellationToken cancellationToken);
    Task<Result<PlanDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<DefaultPlanDto>> GetDefaultAsync(CancellationToken cancellationToken);
    Task<Result<PlanDetailDto>> CreateAsync(SavePlanRequest request, CancellationToken cancellationToken);
    Task<Result<PlanDetailDto>> UpdateAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken);
    Task<Result<PlanDetailDto>> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken);
    Task<Result<PlanAssignmentResultDto>> AssignAsync(Guid organizationId, AssignPlanRequest request, CancellationToken cancellationToken);
    Task<Result<PlanAssignmentResultDto>> RetrySyncAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<PlanAssignmentResultDto>> SetPermissionOverrideAsync(Guid organizationId, string permissionCode, bool enabled, CancellationToken cancellationToken);
}

public interface IAdminOverviewUseCase
{
    Task<Result<DashboardSnapshotDto>> GetDashboardAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<PlatformUserDto>>> GetUsersAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<OrganizationSummaryDto>>> GetOrganizationsAsync(CancellationToken cancellationToken);
    Task<Result<OrganizationDetailDto>> GetOrganizationAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<OrganizationOperationDto>>> GetOperationsAsync(CancellationToken cancellationToken);
}

public interface IOrganizationAdministrationUseCase
{
    Task<Result<OrganizationSummaryDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task<Result<OrganizationDetailDto>> ChangeOwnerAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<Result<PasswordResetResultDto>> RequestPasswordResetAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result<PlatformUserDto>> SetMembershipStatusAsync(Guid membershipId, bool isActive, CancellationToken cancellationToken);
    Task<Result<PlatformUserDto>> ReassignMembershipAsync(ReassignMembershipRequest request, CancellationToken cancellationToken);
}

public interface IOperationStatusSettingsUseCase
{
    Task<Result<OperationStatusSettingsDto>> GetAsync(CancellationToken cancellationToken);
    Task<Result<OperationStatusSettingsDto>> UpdateAsync(UpdateOperationStatusSettingsRequest request, CancellationToken cancellationToken);
}
