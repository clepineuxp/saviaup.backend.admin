using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.Ports;

public interface IAdminIdentityRepository
{
    Task<AdminUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AdminUser>> ListAsync(CancellationToken cancellationToken);
    Task AddAsync(AdminUser user, CancellationToken cancellationToken);
}

public interface IPlanRepository
{
    Task<IReadOnlyCollection<Plan>> ListAsync(CancellationToken cancellationToken);
    Task<Plan?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? excludedId, CancellationToken cancellationToken);
    Task AddAsync(Plan plan, CancellationToken cancellationToken);
    Task<TenantPlanAssignment?> GetAssignmentAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantPlanAssignment>> GetAssignmentsForPlanAsync(Guid planId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, TenantPlanAssignment>> GetAssignmentsAsync(CancellationToken cancellationToken);
    Task AddAssignmentAsync(TenantPlanAssignment assignment, CancellationToken cancellationToken);
    Task<int> CountAssignmentsAsync(Guid planId, CancellationToken cancellationToken);
}

public interface IAuditRepository
{
    Task AddAsync(AdminAuditLog entry, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AdminAuditLog>> ListRecentAsync(int take, CancellationToken cancellationToken);
}

public interface IOperationStatusSettingsRepository
{
    Task<OperationStatusSettings?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(OperationStatusSettings settings, CancellationToken cancellationToken);
}

public interface IAdminUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOperationalAdminPort
{
    Task<OperationalCounts> GetCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PlatformUserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<PlatformUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OperationalOrganization>> GetOrganizationsAsync(CancellationToken cancellationToken);
    Task<OperationalOrganizationDetail?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OrganizationOperationDto>> GetOperationsAsync(OperationalStatusPolicy policy, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OperationalPermission>> GetPermissionCatalogAsync(CancellationToken cancellationToken);
    Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task ReplaceTenantPermissionsAsync(Guid organizationId, IReadOnlyCollection<string> permissionCodes, CancellationToken cancellationToken);
    Task<bool> SetOrganizationStatusAsync(Guid organizationId, bool isActive, CancellationToken cancellationToken);
    Task<Result> ChangeOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<(string Email, string PreferredLanguage)?> GetPasswordResetRecipientAsync(Guid userId, CancellationToken cancellationToken);
    Task AddPasswordResetTokenAsync(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<Result<Guid>> SetMembershipStatusAsync(Guid membershipId, bool isActive, CancellationToken cancellationToken);
    Task<Result<Guid>> ReassignMembershipAsync(Guid userId, Guid membershipId, Guid targetOrganizationId, CancellationToken cancellationToken);
}

public interface IAdminPasswordHasher
{
    string Hash(AdminUser user, string password);
    bool Verify(AdminUser user, string passwordHash, string password);
}

public interface IAdminTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(AdminUser user);
}

public interface ISecureTokenGenerator
{
    string Generate();
    string Hash(string token);
}

public interface IAdminEmailSender
{
    string DeliveryMode { get; }
    Task SendPlatformPasswordResetAsync(string email, string language, string resetLink, CancellationToken cancellationToken);
}

public interface IPasswordResetLinkFactory
{
    string Create(string rawToken);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public interface IAdminActorContext
{
    Guid UserId { get; }
    string Email { get; }
    string? CorrelationId { get; }
}
