using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "role_permissions",
                right => right.HasOne<Permission>().WithMany().HasForeignKey("permission_id").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Role>().WithMany().HasForeignKey("role_id").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("role_id", "permission_id"));
    }
}
