using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Email;

internal sealed class EmailRequests(AppDbContext context, IClock clock) : IEmailRequests
{
    public void Add(EmailRequestKind kind, Guid accountId)
    {
        context.EmailRequests.Add(New(kind, accountId, null));
    }

    public void Add(EmailRequestKind kind, string normalizedEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        context.EmailRequests.Add(New(kind, null, normalizedEmail));
    }

    private EmailRequest New(EmailRequestKind kind, Guid? accountId, string? email)
    {
        Instant now = clock.GetCurrentInstant();
        return new EmailRequest
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            AccountId = accountId,
            Email = email,
            Status = EmailRequestStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        };
    }
}
