using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Deletes expired refresh tokens and the sessions that have no live one left.</summary>
public sealed record PurgeExpiredSessionsCommand : ICommand<Result<PurgeExpiredSessionsResponse>>;
