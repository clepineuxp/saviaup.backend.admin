namespace SaviaUp.Admin.Domain.Entities;

public sealed class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid ActorAdminUserId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Kind { get; set; } = "ORGANIZATION";
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
