using System.Net;

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace WattWise.Api.IntegrationTests;

public class OpenApiDocumentTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_openapi_document_has_no_parse_errors()
    {
        ReadResult result = await LoadAsync();

        Assert.Empty(result.Diagnostic!.Errors);
    }

    [Fact]
    public async Task The_openapi_document_is_titled_Watt_Wise_API_version_v1()
    {
        OpenApiDocument document = (await LoadAsync()).Document!;

        // The version "v1" is characterization: backend/src/WattWise.Api/OpenApi/OpenApiServiceCollectionExtensions.cs:17
        Assert.Equal("Watt-Wise API v1", $"{document.Info.Title} {document.Info.Version}");
    }

    [Fact]
    public async Task The_openapi_document_lists_the_about_get_operation()
    {
        OpenApiDocument document = (await LoadAsync()).Document!;

        Assert.Equal(
            [HttpMethod.Get],
            document.Paths["/api/v1/about"].Operations!.Keys);
    }

    [Fact]
    public async Task The_openapi_document_defines_a_Bearer_JWT_security_scheme()
    {
        OpenApiDocument document = (await LoadAsync()).Document!;

        OpenApiSecurityScheme scheme = Assert.IsType<OpenApiSecurityScheme>(document.Components!.SecuritySchemes!["Bearer"]);
        Assert.Equal((SecuritySchemeType.Http, "bearer", "JWT"), (scheme.Type, scheme.Scheme, scheme.BearerFormat));
    }

    [Fact]
    public async Task The_change_password_operation_declares_its_responses_including_the_409_conflict()
    {
        OpenApiDocument document = (await LoadAsync()).Document!;

        Assert.Equal(
            "200,400,401,404,409,423",
            string.Join(',', document.Paths["/api/v1/auth/change-password"].Operations![HttpMethod.Post].Responses!.Keys.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task The_me_operation_requires_the_Bearer_scheme_and_sign_in_requires_nothing()
    {
        OpenApiDocument document = (await LoadAsync()).Document!;

        Assert.Equal(
            (1, 0),
            (document.Paths["/api/v1/me"].Operations![HttpMethod.Get].Security!.Count,
             document.Paths["/api/v1/auth/sign-in"].Operations![HttpMethod.Post].Security?.Count ?? 0));
    }

    [Fact]
    public async Task Scalar_is_served_as_html_outside_production()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/scalar", Token);

        Assert.Equal("200 text/html", $"{(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType}");
    }

    private async Task<ReadResult> LoadAsync()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using Stream stream = await response.Content.ReadAsStreamAsync(Token);

        return await OpenApiDocument.LoadAsync(stream, "json", settings: null, Token);
    }
}
