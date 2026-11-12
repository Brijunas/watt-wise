using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace WattWise.Api.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
    private const string BearerScheme = "Bearer";

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
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The access token from sign-in or refresh.",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                // Mark the operations that need a signed-in caller; the operations that opt out with AllowAnonymous() stay unmarked.
                IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
