using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed record CreateThingCommand(Address Address) : ICommand<Result<string>>;
