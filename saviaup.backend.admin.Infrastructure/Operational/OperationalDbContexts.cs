using Microsoft.EntityFrameworkCore;

namespace SaviaUp.Admin.Infrastructure.Operational;

internal sealed class OperationalPlatformDbContext(DbContextOptions<OperationalPlatformDbContext> options) : DbContext(options)
{
    public DbSet<OperationalUser> Users => Set<OperationalUser>();
    public DbSet<OperationalTenant> Tenants => Set<OperationalTenant>();
    public DbSet<OperationalMembership> Memberships => Set<OperationalMembership>();
    public DbSet<OperationalModule> Modules => Set<OperationalModule>();
    public DbSet<OperationalPermissionEntity> Permissions => Set<OperationalPermissionEntity>();
    public DbSet<OperationalTenantPermission> TenantPermissions => Set<OperationalTenantPermission>();
    public DbSet<OperationalRefreshToken> RefreshTokens => Set<OperationalRefreshToken>();
    public DbSet<OperationalPasswordResetToken> PasswordResetTokens => Set<OperationalPasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationalUser>(builder =>
        {
            builder.ToTable("users");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Email).HasMaxLength(320).IsRequired();
            builder.Property(item => item.NormalizedEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.PasswordHash).HasMaxLength(1000).IsRequired();
            builder.Property(item => item.FirstName).HasMaxLength(100).IsRequired();
            builder.Property(item => item.LastName).HasMaxLength(100).IsRequired();
            builder.Property(item => item.PreferredLanguage).HasMaxLength(10).IsRequired();
            builder.Ignore(item => item.Memberships);
        });
        modelBuilder.Entity<OperationalTenant>(builder =>
        {
            builder.ToTable("tenants");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
            builder.Property(item => item.ResponsibleName).HasMaxLength(160);
            builder.Property(item => item.Document).HasMaxLength(80);
            builder.Property(item => item.ContactName).HasMaxLength(160);
            builder.Property(item => item.Email).HasMaxLength(320);
            builder.Property(item => item.Address).HasMaxLength(500);
            builder.Property(item => item.Country).HasMaxLength(100);
            builder.Property(item => item.State).HasMaxLength(120);
            builder.Property(item => item.City).HasMaxLength(120);
            builder.Property(item => item.Phone).HasMaxLength(50);
            builder.Property(item => item.Website).HasMaxLength(2048);
            builder.Ignore(item => item.Memberships);
        });
        modelBuilder.Entity<OperationalMembership>(builder =>
        {
            builder.ToTable("tenant_memberships");
            builder.HasKey(item => item.Id);
            builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(item => new { item.UserId, item.TenantId }).IsUnique();
        });
        modelBuilder.Entity<OperationalModule>(builder =>
        {
            builder.ToTable("modules");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Code).HasMaxLength(80).IsRequired();
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        });
        modelBuilder.Entity<OperationalPermissionEntity>(builder =>
        {
            builder.ToTable("permissions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Code).HasMaxLength(160).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(500).IsRequired();
            builder.HasOne(item => item.Module).WithMany().HasForeignKey(item => item.ModuleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OperationalTenantPermission>(builder =>
        {
            builder.ToTable("tenant_permissions");
            builder.HasKey(item => new { item.TenantId, item.PermissionId });
        });
        modelBuilder.Entity<OperationalRefreshToken>(builder =>
        {
            builder.ToTable("refresh_tokens");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
        });
        modelBuilder.Entity<OperationalPasswordResetToken>(builder =>
        {
            builder.ToTable("password_reset_tokens");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
        });
    }
}

internal sealed class OperationalApplicationDbContext(DbContextOptions<OperationalApplicationDbContext> options) : DbContext(options)
{
    public DbSet<OperationalRole> Roles => Set<OperationalRole>();
    public DbSet<OperationalOrder> Orders => Set<OperationalOrder>();
    public DbSet<OperationalRestaurantTable> Tables => Set<OperationalRestaurantTable>();
    public DbSet<OperationalCashRegisterShift> CashRegisterShifts => Set<OperationalCashRegisterShift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationalRole>(builder =>
        {
            builder.ToTable("roles");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Code).HasMaxLength(80).IsRequired();
            builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(500);
        });
        modelBuilder.Entity<OperationalOrder>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Status).HasMaxLength(30).IsRequired();
            builder.Property(item => item.TotalAmount).HasPrecision(18, 2);
            builder.Ignore(item => item.OrderNumber);
        });
        modelBuilder.Entity<OperationalRestaurantTable>(builder =>
        {
            builder.ToTable("restaurant_tables");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        });
        modelBuilder.Entity<OperationalCashRegisterShift>(builder =>
        {
            builder.ToTable("cash_register_shifts");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        });
    }
}
