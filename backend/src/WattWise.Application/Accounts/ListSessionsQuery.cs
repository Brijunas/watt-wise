using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>The account's live sessions, most recently used first.</summary>
/// <param name="AccountId">The account's id, from the access token's <c>sub</c> claim.</param>
/// <param name="CurrentSessionId">The session id from the access token's <c>sid</c> claim; <c>null</c> when there is none.</param>
public sealed record ListSessionsQuery(Guid AccountId, Guid? CurrentSessionId) : IQuery<Result<IReadOnlyList<SessionSummary>>>;
