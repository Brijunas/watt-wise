using Microsoft.OpenApi;

namespace WattWise.Api.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
    /// <summary>Registers the "v1" OpenAPI document, served at /openapi/v1.json.</summary>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            // 3.0 is a version the client generator's parser reads.
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Watt-Wise API";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
        });
        return services;
    }
}
