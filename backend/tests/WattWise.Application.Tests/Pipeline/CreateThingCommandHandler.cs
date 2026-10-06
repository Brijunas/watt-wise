using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class CreateThingCommandHandler(CallRecorder recorder) : ICommandHandler<CreateThingCommand, Result<string>>
{
    public ValueTask<Result<string>> Handle(CreateThingCommand command, CancellationToken cancellationToken)
    {
        recorder.Record();
        return ValueTask.FromResult<Result<string>>(command.Address.StreetName!);
    }
}
