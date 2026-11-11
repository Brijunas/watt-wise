using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityEmailAddressNormalizerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task An_address_is_trimmed_and_lowercased()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();

        string normalized = provider.GetRequiredService<IEmailAddressNormalizer>().Normalize(" User@Example.COM ");

        Assert.Equal("user@example.com", normalized);
    }
}
