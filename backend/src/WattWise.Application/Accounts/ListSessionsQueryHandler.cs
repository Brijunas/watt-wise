using Mediator;

using NodaTime;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ListSessionsQueryHandler(IAccountsQueries queries, IClock clock)
    : IQueryHandler<ListSessionsQuery, Result<IReadOnlyList<SessionSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<SessionSummary>>> Handle(ListSessionsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SessionListItem> items = await queries.GetSessionsAsync(query.AccountId, clock.GetCurrentInstant(), cancellationToken);
        return items
            .Select(item => new SessionSummary(item.Id, item.DeviceLabel, item.SignedInAt, item.LastUsedAt, item.Id == query.CurrentSessionId))
            .ToList();
    }
}
