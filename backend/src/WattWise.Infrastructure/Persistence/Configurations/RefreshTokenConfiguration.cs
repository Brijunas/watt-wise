using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasColumnType("bytea").IsRequired();
        builder.Property(token => token.IssuedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();
        // PostgreSQL's xmin system column: a save that changes a token another request already changed fails, so a token is exchanged once.
        builder.Property<uint>("xmin").IsRowVersion();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.ExpiresAt);
    }
}
