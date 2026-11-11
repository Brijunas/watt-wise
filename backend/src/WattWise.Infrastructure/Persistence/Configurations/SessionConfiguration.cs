using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.DeviceLabel).HasMaxLength(100).IsRequired();
        builder.Property(session => session.SignedInAt).IsRequired();
        builder.Property(session => session.LastUsedAt).IsRequired();

        // Deleting an account deletes its sessions.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(session => session.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<RefreshToken>("_refreshTokens")
            .WithOne()
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_refreshTokens").AutoInclude();
        builder.Ignore(session => session.RefreshTokens);
    }
}
