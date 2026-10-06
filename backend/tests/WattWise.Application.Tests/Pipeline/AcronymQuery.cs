using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

/// <summary>A request whose property names start with acronyms, to check that validation keys keep the validator's property names.</summary>
public sealed record AcronymQuery(string? URL, string? IPAddress) : IQuery<Result<string>>;
