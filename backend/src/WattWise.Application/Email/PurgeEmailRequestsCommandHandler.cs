using Mediator;

using NodaTime;

using WattWise.Application.Results;

namespace WattWise.Application.Email;

public sealed class PurgeEmailRequestsCommandHandler(IEmailOutbox outbox, IClock clock)
    : ICommandHandler<PurgeEmailRequestsCommand, Result<PurgeEmailRequestsResponse>>
{
    public static readonly Duration Retention = Duration.FromDays(30);

    public async ValueTask<Result<PurgeEmailRequestsResponse>> Handle(PurgeEmailRequestsCommand command, CancellationToken cancellationToken)
    {
        int deleted = await outbox.PurgeAsync(clock.GetCurrentInstant() - Retention, cancellationToken);
        return new PurgeEmailRequestsResponse(deleted);
    }
}
