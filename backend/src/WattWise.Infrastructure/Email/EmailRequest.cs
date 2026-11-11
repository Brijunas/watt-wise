using NodaTime;

using WattWise.Application.Email;

namespace WattWise.Infrastructure.Email;

/// <summary>
/// One row of the email outbox. A technical detail, not a Domain entity. Exactly one of
/// <see cref="AccountId"/> and <see cref="Email"/> is set. Never holds a token, a link or a server message.
/// </summary>
public sealed class EmailRequest
{
    public required Guid Id { get; init; }

    public required EmailRequestKind Kind { get; init; }

    public Guid? AccountId { get; init; }

    /// <summary>The normalized address, for requests that are keyed by one.</summary>
    public string? Email { get; init; }

    public required EmailRequestStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public required Instant NextAttemptAt { get; set; }

    public Instant? LastAttemptAt { get; set; }

    /// <summary>An exception type name or an SMTP status, at most 100 characters.</summary>
    public string? LastError { get; set; }

    public required Instant CreatedAt { get; init; }

    public Instant? SentAt { get; set; }
}
