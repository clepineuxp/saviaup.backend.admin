namespace SaviaUp.Admin.Domain.Entities;

public sealed class Plan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "COP";
    public string Status { get; set; } = PlanStatuses.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<PlanPermission> Permissions { get; set; } = [];
    public ICollection<PlanPriceHistory> PriceHistory { get; set; } = [];
}

public sealed class PlanPermission
{
    public Guid PlanId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public Plan Plan { get; set; } = null!;
}

public sealed class PlanPriceHistory
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "COP";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public Plan Plan { get; set; } = null!;
}

public static class PlanStatuses
{
    public const string Active = "ACTIVE";
    public const string Draft = "DRAFT";
    public const string Archived = "ARCHIVED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Active,
        Draft,
        Archived
    };
}
