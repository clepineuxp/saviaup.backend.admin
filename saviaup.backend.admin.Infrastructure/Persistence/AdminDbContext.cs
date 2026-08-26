using Microsoft.EntityFrameworkCore;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Infrastructure.Persistence;

public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanPermission> PlanPermissions => Set<PlanPermission>();
    public DbSet<PlanPriceHistory> PlanPriceHistory => Set<PlanPriceHistory>();
    public DbSet<TenantPlanAssignment> TenantPlanAssignments => Set<TenantPlanAssignment>();
    public DbSet<TenantPermissionOverride> TenantPermissionOverrides => Set<TenantPermissionOverride>();
    public DbSet<AdminAuditLog> AuditLogs => Set<AdminAuditLog>();
    public DbSet<OperationStatusSettings> OperationStatusSettings => Set<OperationStatusSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(builder =>
        {
            builder.ToTable("admin_users");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Email).HasMaxLength(320).IsRequired();
            builder.Property(item => item.NormalizedEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.PasswordHash).HasMaxLength(1000).IsRequired();
            builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
            builder.Property(item => item.RoleCode).HasMaxLength(40).IsRequired();
            builder.HasIndex(item => item.NormalizedEmail).IsUnique();
            builder.HasIndex(item => item.IsActive);
        });

        modelBuilder.Entity<Plan>(builder =>
        {
            builder.ToTable("plans");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Code).HasMaxLength(40).IsRequired();
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            builder.Property(item => item.MonthlyPrice).HasPrecision(18, 2);
            builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
            builder.HasIndex(item => item.Code).IsUnique();
            builder.HasIndex(item => item.Status);
        });

        modelBuilder.Entity<PlanPermission>(builder =>
        {
            builder.ToTable("plan_permissions");
            builder.HasKey(item => new { item.PlanId, item.PermissionCode });
            builder.Property(item => item.PermissionCode).HasMaxLength(160).IsRequired();
            builder.HasOne(item => item.Plan).WithMany(plan => plan.Permissions).HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(item => item.PermissionCode);
        });

        modelBuilder.Entity<PlanPriceHistory>(builder =>
        {
            builder.ToTable("plan_price_history");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.MonthlyPrice).HasPrecision(18, 2);
            builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            builder.HasOne(item => item.Plan).WithMany(plan => plan.PriceHistory).HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(item => new { item.PlanId, item.EffectiveFrom });
        });

        modelBuilder.Entity<TenantPlanAssignment>(builder =>
        {
            builder.ToTable("tenant_plan_assignments");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.SyncStatus).HasMaxLength(20).IsRequired();
            builder.Property(item => item.LastSyncErrorCode).HasMaxLength(120);
            builder.HasIndex(item => item.TenantId).IsUnique();
            builder.HasIndex(item => item.PlanId);
            builder.HasIndex(item => item.SyncStatus);
            builder.HasOne(item => item.Plan).WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TenantPermissionOverride>(builder =>
        {
            builder.ToTable("tenant_permission_overrides");
            builder.HasKey(item => new { item.AssignmentId, item.PermissionCode });
            builder.Property(item => item.PermissionCode).HasMaxLength(160).IsRequired();
            builder.HasOne(item => item.Assignment).WithMany(assignment => assignment.Overrides).HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAuditLog>(builder =>
        {
            builder.ToTable("admin_audit_logs");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.ActorEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.Action).HasMaxLength(160).IsRequired();
            builder.Property(item => item.SubjectType).HasMaxLength(80).IsRequired();
            builder.Property(item => item.SubjectId).HasMaxLength(160).IsRequired();
            builder.Property(item => item.SubjectName).HasMaxLength(320).IsRequired();
            builder.Property(item => item.Detail).HasMaxLength(2000).IsRequired();
            builder.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            builder.Property(item => item.CorrelationId).HasMaxLength(120);
            builder.HasIndex(item => item.OccurredAt);
            builder.HasIndex(item => item.ActorAdminUserId);
            builder.HasIndex(item => new { item.SubjectType, item.SubjectId });
        });

        modelBuilder.Entity<OperationStatusSettings>(builder =>
        {
            builder.ToTable("operation_status_settings");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.InactivitySeverity).HasMaxLength(20).IsRequired();
            builder.Property(item => item.CashRegisterSeverity).HasMaxLength(20).IsRequired();
        });
    }
}
