using Microsoft.Extensions.Options;

namespace WattWise.Api.IntegrationTests;

public class ObservabilityValidationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://collector:4317")]
    [InlineData("http://collector:4317?x=1")]
    [InlineData("http://collector:4317/v1/traces")]
    [InlineData("http://user@collector:4317")]
    public async Task Malformed_otlp_endpoint_fails_host_start(string endpoint)
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Observability:OtlpEndpoint"] = endpoint,
        };
        await using ApiFactory factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        OptionsValidationException validation = Assert.IsType<OptionsValidationException>(
            ExceptionChain.Of(exception).FirstOrDefault(e => e is OptionsValidationException));
        Assert.Contains("Observability:OtlpEndpoint", validation.Message, StringComparison.Ordinal);
    }
}
