namespace SaviaUp.Admin.Domain.Entities;

public sealed class OperationStatusSettings
{
    public static readonly Guid SingletonId = Guid.Parse("9d829759-a1ce-4e61-91da-973b1fe725bd");

    public Guid Id { get; set; } = SingletonId;
    public bool InactivityRuleEnabled { get; set; } = true;
    public int InactivityThresholdMinutes { get; set; } = 120;
    public string InactivitySeverity { get; set; } = OperationIssueSeverities.Critical;
    public bool CashRegisterRuleEnabled { get; set; } = true;
    public string CashRegisterSeverity { get; set; } = OperationIssueSeverities.Warning;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByAdminUserId { get; set; }
}

public static class OperationIssueSeverities
{
    public const string Warning = "WARNING";
    public const string Critical = "CRITICAL";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Warning,
        Critical
    };
}
