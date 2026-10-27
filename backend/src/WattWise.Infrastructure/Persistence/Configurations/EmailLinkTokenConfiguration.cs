using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class EmailLinkTokenConfiguration : IEntityTypeConfiguration<EmailLinkToken>
{
    public void Configure(EntityTypeBuilder<EmailLinkToken> builder)
    {
        builder.ToTable("email_link_tokens");

        // One live link per AccountIdentity and kind.
        builder.HasKey(token => new { token.AccountIdentityId, token.Purpose });
        builder.Property(token => token.AccountIdentityId).HasColumnName("identity_id");
        builder.Property(token => token.TokenHash).HasColumnType("bytea").IsRequired();

        builder.HasOne<AccountIdentity>()
            .WithMany()
            .HasForeignKey(token => token.AccountIdentityId)
            .HasConstraintName("fk_email_link_tokens_identity_users_identity_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
