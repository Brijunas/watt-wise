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
    public void Malformed_origin_fails_host_start(string origin)
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Cors:AllowedOrigins:0"] = origin,
        };
        using ApiFactory factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(Chain(exception), e => e is OptionsValidationException);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
