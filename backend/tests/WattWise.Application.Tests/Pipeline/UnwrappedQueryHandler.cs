using Mediator;

namespace WattWise.Application.Tests.Pipeline;

public sealed class UnwrappedQueryHandler : IQueryHandler<UnwrappedQuery, string>
{
    public ValueTask<string> Handle(UnwrappedQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult("plain");
    }
}
