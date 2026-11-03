namespace WattWise.Testing;

/// <summary>A message read back from Mailpit's API (<c>/api/v1/message/{id}</c>).</summary>
public sealed record MailpitMessage(
    string Id,
    string MessageId,
    MailpitAddress From,
    IReadOnlyList<MailpitAddress> To,
    string Subject,
    string Text,
    string Html);
