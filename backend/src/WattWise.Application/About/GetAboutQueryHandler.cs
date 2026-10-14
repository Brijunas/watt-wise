using System.Reflection;

using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.About;

public sealed class GetAboutQueryHandler : IQueryHandler<GetAboutQuery, Result<AboutResponse>>
{
    public const string ApiName = "Watt-Wise API";
    public const string FallbackVersion = "0.0.0";

    public ValueTask<Result<AboutResponse>> Handle(GetAboutQuery query, CancellationToken cancellationToken)
    {
        string? version = typeof(ApplicationAssembly).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return ValueTask.FromResult<Result<AboutResponse>>(
            new AboutResponse(ApiName, string.IsNullOrWhiteSpace(version) ? FallbackVersion : version));
    }
}
