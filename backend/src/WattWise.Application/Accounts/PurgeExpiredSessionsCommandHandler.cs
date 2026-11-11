using Mediator;

using NodaTime;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class PurgeExpiredSessionsCommandHandler(IExpiredSessions expiredSessions, IClock clock)
    : ICommandHandler<PurgeExpiredSessionsCommand, Result<PurgeExpiredSessionsResponse>>
{
    public async ValueTask<Result<PurgeExpiredSessionsResponse>> Handle(PurgeExpiredSessionsCommand command, CancellationToken cancellationToken)
    {
        int deleted = await expiredSessions.PurgeAsync(clock.GetCurrentInstant(), cancellationToken);
        return new PurgeExpiredSessionsResponse(deleted);
    }
}
