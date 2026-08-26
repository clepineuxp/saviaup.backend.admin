namespace SaviaUp.Admin.Application.Contracts;

public sealed record AdminSessionDto(AdminIdentityDto Administrator, string AccessToken, DateTimeOffset ExpiresAt);
public sealed record AdminIdentityDto(Guid Id, string Name, string Email, string RoleCode, bool IsActive, DateTimeOffset? LastLoginAt);
public sealed record LoginRequest(string Email, string Password);
public sealed record CreateAdminUserRequest(string Name, string Email, string Password, string RoleCode);
public sealed record SetStatusRequest(bool IsActive);

public sealed record MembershipSummaryDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string RoleCode,
    string RoleName,
    bool IsActive,
    DateTimeOffset? DisabledUntil);

public sealed record PlatformUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAccessAt,
    IReadOnlyCollection<MembershipSummaryDto> Memberships);

public sealed record OwnerSummaryDto(Guid UserId, string Name, string Email);
public sealed record PlanSummaryDto(Guid Id, string Name, decimal MonthlyPrice, string Currency);

public sealed record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string LegalName,
    string Slug,
    bool IsActive,
    OwnerSummaryDto Owner,
    int MemberCount,
    int ActivePermissionCount,
    int TotalPermissionCount,
    PlanSummaryDto? Plan,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActivityAt,
    string Health);

public sealed record TenantPermissionDto(
    string Code,
    string Name,
    string Description,
    string GroupCode,
    string GroupName,
    bool Enabled);

public sealed record OrganizationMemberDto(
    Guid MembershipId,
    Guid UserId,
    string Name,
    string Email,
    string RoleCode,
    string RoleName,
    bool IsActive,
    DateTimeOffset? DisabledUntil,
    DateTimeOffset? LastAccessAt);

public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string LegalName,
    string Slug,
    bool IsActive,
    OwnerSummaryDto Owner,
    int MemberCount,
    int ActivePermissionCount,
    int TotalPermissionCount,
    PlanSummaryDto? Plan,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActivityAt,
    string Health,
    string DocumentNumber,
    string ContactEmail,
    string City,
    IReadOnlyCollection<TenantPermissionDto> Permissions,
    IReadOnlyCollection<OrganizationMemberDto> Members,
    string PermissionSyncStatus,
    DateTimeOffset? LastPermissionsSyncAt);

public sealed record OperationIssueDto(string Severity, string Message);

public sealed record OrganizationOperationDto(
    Guid OrganizationId,
    string OrganizationName,
    string Health,
    string ApiStatus,
    decimal TodaySales,
    int TodayOrders,
    decimal AverageTicket,
    int OccupiedTables,
    int TotalTables,
    int OpenCashRegisters,
    string CashRegisterStatus,
    DateTimeOffset? LastOrderAt,
    IReadOnlyCollection<OperationIssueDto> Issues);

public sealed record PlatformPlanDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    decimal MonthlyPrice,
    string Currency,
    string Status,
    int OrganizationCount,
    int IncludedPermissionCount);

public sealed record PlanPriceHistoryDto(Guid Id, decimal MonthlyPrice, string Currency, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil);
public sealed record PlanPermissionOptionDto(string Code, string Description, string ModuleCode, string ModuleName);

public sealed record PlanDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    decimal MonthlyPrice,
    string Currency,
    string Status,
    IReadOnlyCollection<string> PermissionCodes,
    IReadOnlyCollection<PlanPriceHistoryDto> PriceHistory,
    int OrganizationCount);

public sealed record SavePlanRequest(
    string Code,
    string Name,
    string Description,
    decimal MonthlyPrice,
    string Currency,
    string Status,
    IReadOnlyCollection<string> PermissionCodes);

public sealed record SetPlanStatusRequest(string Status);
public sealed record AssignPlanRequest(Guid PlanId, bool PreserveOverrides = false);
public sealed record PlanAssignmentResultDto(Guid OrganizationId, Guid? PlanId, string SyncStatus, DateTimeOffset? LastSyncedAt);

public sealed record AuditEventDto(
    Guid Id,
    string Action,
    string Subject,
    string Detail,
    DateTimeOffset OccurredAt,
    string Kind);

public sealed record DashboardSnapshotDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalOrganizations,
    int ActiveOrganizations,
    int OrganizationsWithAlerts,
    decimal TodayGrossVolume,
    int TodayOrders,
    decimal MonthlyRecurringRevenue,
    decimal OrganizationGrowth,
    IReadOnlyCollection<AuditEventDto> RecentEvents,
    IReadOnlyCollection<OrganizationOperationDto> OrganizationHealth);

public sealed record PasswordResetResultDto(string MaskedEmail, string DeliveryMode);
public sealed record SetPermissionRequest(bool Enabled);
public sealed record ChangeOwnerRequest(Guid UserId);
public sealed record ReassignMembershipRequest(Guid UserId, Guid MembershipId, Guid TargetOrganizationId);
