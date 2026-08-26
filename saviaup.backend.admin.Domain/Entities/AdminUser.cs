namespace SaviaUp.Admin.Domain.Entities;

public sealed class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoleCode { get; set; } = AdminRoleCodes.SuperAdmin;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class AdminRoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Support = "SUPPORT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin,
        Support
    };
}
