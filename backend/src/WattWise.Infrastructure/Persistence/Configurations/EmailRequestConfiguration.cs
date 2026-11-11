using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class EmailRequestConfiguration : IEntityTypeConfiguration<EmailRequest>
{
    public void Configure(EntityTypeBuilder<EmailRequest> builder)
    {
        builder.ToTable("email_requests", table =>
        {
            table.HasCheckConstraint("ck_email_requests_target", "num_nonnulls(account_id, email) = 1");
            table.HasCheckConstraint(
                "ck_email_requests_status",
                "status IN ('pending', 'sending', 'sent', 'superseded', 'failed')");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Kind).HasConversion<string>().IsRequired();
        builder.Property(request => request.Email);
        builder.Property(request => request.Status)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                text => Enum.Parse<EmailRequestStatus>(text, true))
            .IsRequired();
        builder.Property(request => request.AttemptCount).HasDefaultValue(0);
        builder.Property(request => request.LastError).HasMaxLength(100);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(request => request.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Pending rows are the due ones; sending rows are the stuck-row scan.
        builder.HasIndex(request => new { request.Status, request.NextAttemptAt })
            .HasDatabaseName("ix_email_requests_due")
            .HasFilter("status IN ('pending', 'sending')");
    }
}
