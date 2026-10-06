using Microsoft.Extensions.Options;

namespace WattWise.Api.IntegrationTests;

public class CorsValidationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData("https://app.example.test/path")]
    [InlineData("app.example.test")]
    [InlineData("ftp://app.example.test")]
    [InlineData("https://app.example.test?x=1")]
    [InlineData(" ")]
    public async Task Malformed_origin_fails_host_start(string origin)
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Cors:AllowedOrigins:0"] = origin,
        };
        await using ApiFactory factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(ExceptionChain.Of(exception), e => e is OptionsValidationException);
    }
}
