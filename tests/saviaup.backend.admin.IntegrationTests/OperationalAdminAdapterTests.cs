using Microsoft.EntityFrameworkCore;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Infrastructure.Operational;

namespace SaviaUp.Admin.IntegrationTests;

public sealed class OperationalAdminAdapterTests
{
    [Fact]
    public async Task ReplaceTenantPermissions_preserves_existing_permissions_and_applies_only_differences()
    {
        var tenantId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var retainedPermissionId = Guid.NewGuid();
        var removedPermissionId = Guid.NewGuid();
        var addedPermissionId = Guid.NewGuid();
        var ownerRoleId = Guid.NewGuid();
        var customRoleId = Guid.NewGuid();
        var platformOptions = new DbContextOptionsBuilder<OperationalPlatformDbContext>()
            .UseInMemoryDatabase($"platform-{Guid.NewGuid()}")
            .Options;
        var applicationOptions = new DbContextOptionsBuilder<OperationalApplicationDbContext>()
            .UseInMemoryDatabase($"application-{Guid.NewGuid()}")
            .Options;

        await using var platformContext = new OperationalPlatformDbContext(platformOptions);
        await using var applicationContext = new OperationalApplicationDbContext(applicationOptions);
        var now = DateTimeOffset.UtcNow;
        var module = new OperationalModule { Id = moduleId, Code = "operation", Name = "Operation", IsActive = true };
        var tenant = new OperationalTenant { Id = tenantId, Name = "Tenant", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var retainedPermission = new OperationalPermissionEntity
        {
            Id = retainedPermissionId,
            ModuleId = moduleId,
            Module = module,
            Code = "operation.read",
            Description = "Read operation"
        };
        var removedPermission = new OperationalPermissionEntity
        {
            Id = removedPermissionId,
            ModuleId = moduleId,
            Module = module,
            Code = "operation.manage",
            Description = "Manage operation"
        };
        var addedPermission = new OperationalPermissionEntity
        {
            Id = addedPermissionId,
            ModuleId = moduleId,
            Module = module,
            Code = "operation.configure",
            Description = "Configure operation"
        };

        platformContext.AddRange(module, tenant, retainedPermission, removedPermission, addedPermission);
        platformContext.TenantPermissions.AddRange(
            new OperationalTenantPermission { TenantId = tenantId, PermissionId = retainedPermissionId },
            new OperationalTenantPermission { TenantId = tenantId, PermissionId = removedPermissionId });
        await platformContext.SaveChangesAsync();
        platformContext.ChangeTracker.Clear();

        applicationContext.Roles.AddRange(
            new OperationalRole
            {
                Id = ownerRoleId,
                TenantId = tenantId,
                Code = "TENANT_OWNER",
                Name = "Owner",
                IsSystem = true,
                IsActive = true,
                CreatedAt = now
            },
            new OperationalRole
            {
                Id = customRoleId,
                TenantId = tenantId,
                Code = "CUSTOM",
                Name = "Custom",
                IsActive = true,
                CreatedAt = now
            });
        applicationContext.RolePermissions.AddRange(
            new OperationalRolePermission { RoleId = ownerRoleId, PermissionId = retainedPermissionId },
            new OperationalRolePermission { RoleId = ownerRoleId, PermissionId = removedPermissionId },
            new OperationalRolePermission { RoleId = customRoleId, PermissionId = removedPermissionId });
        await applicationContext.SaveChangesAsync();
        applicationContext.ChangeTracker.Clear();

        var sut = new OperationalAdminAdapter(platformContext, applicationContext, new TestDateTimeProvider(now));

        await sut.ReplaceTenantPermissionsAsync(
            tenantId,
            [retainedPermission.Code, addedPermission.Code],
            CancellationToken.None);

        var resultingPermissionIds = await platformContext.TenantPermissions.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.PermissionId)
            .OrderBy(item => item)
            .ToArrayAsync();

        Assert.Equal(new[] { retainedPermissionId, addedPermissionId }.OrderBy(item => item), resultingPermissionIds);

        var resultingOwnerPermissionIds = await applicationContext.RolePermissions.AsNoTracking()
            .Where(item => item.RoleId == ownerRoleId)
            .Select(item => item.PermissionId)
            .OrderBy(item => item)
            .ToArrayAsync();
        Assert.Equal(new[] { retainedPermissionId, addedPermissionId }.OrderBy(item => item), resultingOwnerPermissionIds);
        Assert.True(await applicationContext.RolePermissions.AsNoTracking()
            .AnyAsync(item => item.RoleId == customRoleId && item.PermissionId == removedPermissionId));
    }

    private sealed class TestDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
