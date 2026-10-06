using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed record EchoQuery(string? Name) : IQuery<Result<string>>;
