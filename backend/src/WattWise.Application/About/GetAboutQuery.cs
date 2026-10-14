using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.About;

/// <summary>Asks for the API's name and version.</summary>
public sealed record GetAboutQuery : IQuery<Result<AboutResponse>>;
