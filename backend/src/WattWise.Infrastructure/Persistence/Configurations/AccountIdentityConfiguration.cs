using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

/// <summary>Names the Identity tables like the rest of the schema and adds what the account model needs on top of Identity's defaults.</summary>
internal sealed class AccountIdentityConfiguration :
    IEntityTypeConfiguration<AccountIdentity>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<AccountIdentity> builder)
    {
        builder.ToTable("identity_users");
        builder.Property(identity => identity.AccountId).IsRequired();

        // One identity per account in MVP; deleting the account deletes it.
        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<AccountIdentity>(identity => identity.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Identity's EmailIndex is not unique; UserManager's check can't close the race, the database does.
        builder.HasIndex(identity => identity.NormalizedEmail)
            .HasDatabaseName("ix_identity_users_normalized_email")
            .IsUnique();
        builder.HasIndex(identity => identity.NormalizedUserName)
            .HasDatabaseName("ix_identity_users_normalized_user_name")
            .IsUnique();
    }

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("identity_user_logins");
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("identity_user_tokens");
    }
}
