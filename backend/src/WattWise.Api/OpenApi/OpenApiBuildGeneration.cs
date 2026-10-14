using System.Reflection;

namespace WattWise.Api.OpenApi;

/// <summary>
/// Build-time generation of the OpenAPI document (Microsoft.Extensions.ApiDescription.Server) starts the
/// host with no configuration, so startup validation of the settings can't pass. The generator's entry
/// assembly is <c>GetDocument.Insider</c>, which tells the two cases apart.
/// </summary>
public static class OpenApiBuildGeneration
{
    public static bool IsRunning { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
