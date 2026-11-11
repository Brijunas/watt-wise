using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Email;

/// <summary>One pass of the email worker: recovers stuck rows, claims the due ones and sends them.</summary>
public sealed record SendDueEmailsCommand : ICommand<Result<SendDueEmailsResponse>>;

/// <summary>What a pass did.</summary>
/// <param name="Sent">Emails accepted by the server.</param>
/// <param name="Retried">Emails sent back to pending for a later attempt.</param>
/// <param name="Failed">Emails that failed for good.</param>
public sealed record SendDueEmailsResponse(int Sent, int Retried, int Failed);
