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

        builder.HasMany<RolePermission>("_permissions")
            .WithOne()
            .HasForeignKey(link => link.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_permissions").AutoInclude();
        builder.Ignore(role => role.PermissionIds);
    }
}
