using System.Net;

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace WattWise.Api.IntegrationTests;

public class OpenApiDocumentTests(ErrorsApiFixture fixture) : IClassFixture<ErrorsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Document_is_valid_and_describes_the_api_under_the_v1_prefix()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using Stream stream = await response.Content.ReadAsStreamAsync(Token);
        (OpenApiDocument? document, OpenApiDiagnostic? diagnostic) = await OpenApiDocument.LoadAsync(
            stream, "json", settings: null, Token);
        Assert.NotNull(document);
        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Errors);
        Assert.Equal("Watt-Wise API", document.Info.Title);
        Assert.Equal("v1", document.Info.Version);
        Assert.Contains("/api/v1/test/errors/ok", document.Paths.Keys);
    }

    [Fact]
    public async Task Scalar_ui_is_served_as_html()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/scalar", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
