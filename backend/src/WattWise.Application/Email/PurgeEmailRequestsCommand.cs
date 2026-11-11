using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Email;

/// <summary>Deletes finished outbox rows that are more than 30 days old.</summary>
public sealed record PurgeEmailRequestsCommand : ICommand<Result<PurgeEmailRequestsResponse>>;

/// <summary>The number of rows deleted.</summary>
public sealed record PurgeEmailRequestsResponse(int Deleted);
