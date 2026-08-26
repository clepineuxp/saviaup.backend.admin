namespace SaviaUp.Admin.Domain.Entities;

public sealed class TenantPlanAssignment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string SyncStatus { get; set; } = PermissionSyncStatuses.Pending;
    public string? LastSyncErrorCode { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<TenantPermissionOverride> Overrides { get; set; } = [];
}

public sealed class TenantPermissionOverride
{
    public Guid AssignmentId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public TenantPlanAssignment Assignment { get; set; } = null!;
}

public static class PermissionSyncStatuses
{
    public const string Pending = "PENDING";
    public const string Synced = "SYNCED";
    public const string Failed = "FAILED";
}
