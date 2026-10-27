using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(link => new { link.RoleId, link.PermissionId });
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(link => link.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
