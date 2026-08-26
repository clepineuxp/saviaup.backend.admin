namespace SaviaUp.Admin.Application.Contracts;

public sealed record OperationalPermission(
    Guid Id,
    string Code,
    string Description,
    string ModuleCode,
    string ModuleName,
    bool ModuleIsActive);

public sealed record OperationalOrganization(
    Guid Id,
    string Name,
    string LegalName,
    string DocumentNumber,
    string ContactEmail,
    string City,
    bool IsActive,
    DateTimeOffset CreatedAt,
    OwnerSummaryDto Owner,
    int MemberCount,
    IReadOnlyCollection<string> EnabledPermissionCodes,
    int TotalPermissionCount,
    DateTimeOffset? LastActivityAt);

public sealed record OperationalOrganizationDetail(
    OperationalOrganization Organization,
    IReadOnlyCollection<OrganizationMemberDto> Members,
    IReadOnlyCollection<OperationalPermission> PermissionCatalog);

public sealed record OperationalCounts(int TotalUsers, int ActiveUsers, int TotalOrganizations, int ActiveOrganizations);

public sealed record OperationalStatusPolicy(
    bool InactivityRuleEnabled,
    int InactivityThresholdMinutes,
    string InactivitySeverity,
    bool CashRegisterRuleEnabled,
    string CashRegisterSeverity);
