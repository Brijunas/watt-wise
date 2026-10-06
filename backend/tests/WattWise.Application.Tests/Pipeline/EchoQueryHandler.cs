using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class EchoQueryHandler(CallRecorder recorder) : IQueryHandler<EchoQuery, Result<string>>
{
    public ValueTask<Result<string>> Handle(EchoQuery query, CancellationToken cancellationToken)
    {
        recorder.Record();
        return ValueTask.FromResult<Result<string>>($"Hello, {query.Name}");
    }
}
