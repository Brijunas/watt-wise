using WattWise.Application.Email;

namespace WattWise.Application.Tests.Accounts;

/// <summary>A hand-written outbox write port that logs each request.</summary>
internal sealed class RecordingEmailRequests(List<string> calls) : IEmailRequests
{
    public void Add(EmailRequestKind kind, Guid accountId) => calls.Add($"Add:{kind}:{accountId}");

    public void Add(EmailRequestKind kind, string normalizedEmail) => calls.Add($"Add:{kind}:{normalizedEmail}");
}
