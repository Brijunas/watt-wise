using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class ThrowingQueryHandler : IQueryHandler<ThrowingQuery, Result<string>>
{
    public ValueTask<Result<string>> Handle(ThrowingQuery query, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Boom.");
    }
}
