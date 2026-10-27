using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class AccountRoleConfiguration : IEntityTypeConfiguration<AccountRole>
{
    public void Configure(EntityTypeBuilder<AccountRole> builder)
    {
        builder.ToTable("account_roles");
        builder.HasKey(link => new { link.AccountId, link.RoleId });
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(link => link.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
