using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Infrastructure.Identity;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class EmailLinkTokenConfiguration : IEntityTypeConfiguration<EmailLinkToken>
{
    public void Configure(EntityTypeBuilder<EmailLinkToken> builder)
    {
        builder.ToTable("email_link_tokens");

        // One live link per identity and kind.
        builder.HasKey(token => new { token.IdentityId, token.Purpose });
        builder.Property(token => token.TokenHash).HasColumnType("bytea").IsRequired();

        builder.HasOne<AccountIdentity>()
            .WithMany()
            .HasForeignKey(token => token.IdentityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
