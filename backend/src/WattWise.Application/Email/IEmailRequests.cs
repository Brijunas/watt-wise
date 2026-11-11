namespace WattWise.Application.Email;

/// <summary>
/// The write side of the email outbox. A use case records a request with the same unit of work as its
/// account change; nothing is saved here and nothing touches SMTP. The Jobs host sends the request later.
/// </summary>
public interface IEmailRequests
{
    /// <summary>Records a request for an account that exists. The row is saved by the caller's <c>SaveChangesAsync</c>.</summary>
    void Add(EmailRequestKind kind, Guid accountId);

    /// <summary>
    /// Records a request keyed by an address, without looking the address up; the worker resolves it.
    /// <paramref name="normalizedEmail"/> must already be normalized with the account email rules.
    /// </summary>
    void Add(EmailRequestKind kind, string normalizedEmail);
}
