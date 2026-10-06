using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed record NotFoundQuery : IQuery<Result<string>>;
