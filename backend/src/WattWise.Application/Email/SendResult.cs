namespace WattWise.Application.Email;

/// <summary>
/// The outcome of one send attempt. <see cref="Error"/> is what the outbox stores as <c>last_error</c>: an exception
/// type name or an SMTP status (for example <c>SmtpCommandException 451</c>), never a message or an address; null when sent.
/// </summary>
public sealed record SendResult(SendOutcome Outcome, string? Error)
{
    public static SendResult Sent { get; } = new(SendOutcome.Sent, null);

    public static SendResult Retryable(string error) => new(SendOutcome.Retryable, error);

    public static SendResult Permanent(string error) => new(SendOutcome.Permanent, error);
}
