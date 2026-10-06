using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed record ThrowingQuery : IQuery<Result<string>>;
