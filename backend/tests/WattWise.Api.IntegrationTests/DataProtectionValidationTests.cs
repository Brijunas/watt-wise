using Microsoft.Extensions.Options;

namespace WattWise.Api.IntegrationTests;

public class DataProtectionValidationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Missing_certificate_fails_host_start()
    {
        Dictionary<string, string?> settings = new(fixture.Database.ApiConfiguration());
        settings.Remove("DataProtection:Certificate");

        await AssertStartFails(settings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a certificate")]
    [InlineData("-----BEGIN CERTIFICATE-----\nnot base64!\n-----END CERTIFICATE-----")]
    public async Task Unparsable_certificate_fails_host_start(string pem)
    {
        Dictionary<string, string?> settings = new(fixture.Database.ApiConfiguration())
        {
            ["DataProtection:Certificate"] = pem,
        };

        await AssertStartFails(settings);
    }

    [Fact]
    public async Task Certificate_without_private_key_fails_host_start()
    {
        Dictionary<string, string?> settings = new(fixture.Database.ApiConfiguration())
        {
            ["DataProtection:Certificate"] = TestCertificates.CertificateOnlyPem,
        };

        await AssertStartFails(settings);
    }

    private static async Task AssertStartFails(Dictionary<string, string?> settings)
    {
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        OptionsValidationException validation = Assert.IsType<OptionsValidationException>(
            ExceptionChain.Of(exception).FirstOrDefault(e => e is OptionsValidationException));
        Assert.Contains("DataProtection:Certificate", validation.Message, StringComparison.Ordinal);
    }
}
