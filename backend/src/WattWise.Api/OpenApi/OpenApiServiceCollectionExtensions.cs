namespace WattWise.Api.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
    /// <summary>Registers the "v1" OpenAPI document, served at /openapi/v1.json.</summary>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Watt-Wise API";
            document.Info.Version = "v1";
            return Task.CompletedTask;
        }));
        return services;
    }
}
