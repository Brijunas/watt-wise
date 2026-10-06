using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class NotFoundQueryHandler : IQueryHandler<NotFoundQuery, Result<string>>
{
    public ValueTask<Result<string>> Handle(NotFoundQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Result<string>>(Error.NotFound("Test.Missing", "The thing is missing."));
    }
}
