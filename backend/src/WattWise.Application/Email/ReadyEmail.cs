namespace WattWise.Application.Email;

/// <summary>A claimed request resolved to a message that can be sent now, with its link already created.</summary>
/// <param name="RequestId">The outbox row's id.</param>
/// <param name="Kind">The kind of the request.</param>
/// <param name="AttemptCount">The number of the attempt about to be made, starting at 1 (already counted by the claim).</param>
/// <param name="Message">The message in the account's language; its link, if any, holds the plaintext token, which exists only in memory.</param>
public sealed record ReadyEmail(Guid RequestId, EmailRequestKind Kind, int AttemptCount, EmailMessage Message);
