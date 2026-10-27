using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id).ValueGeneratedNever();
        builder.Property(permission => permission.Name).IsRequired();
        builder.Property(permission => permission.Description).IsRequired();
        builder.HasIndex(permission => permission.Name).IsUnique();
    }
}
