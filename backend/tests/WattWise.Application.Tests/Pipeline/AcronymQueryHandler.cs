using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class AcronymQueryHandler(CallRecorder recorder) : IQueryHandler<AcronymQuery, Result<string>>
{
    public ValueTask<Result<string>> Handle(AcronymQuery query, CancellationToken cancellationToken)
    {
        recorder.Record();
        return ValueTask.FromResult<Result<string>>($"{query.URL} {query.IPAddress}");
    }
}
