using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountPortRegistrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData(typeof(IPasswordSignIn), "IdentityPasswordSignIn")]
    [InlineData(typeof(ISessionRepository), "SessionRepository")]
    [InlineData(typeof(IAccountsQueries), "AccountsQueries")]
    [InlineData(typeof(IExpiredSessions), "ExpiredSessions")]
    [InlineData(typeof(IAccessTokenIssuer), "JwtAccessTokenIssuer")]
    [InlineData(typeof(IRefreshTokenFactory), "RefreshTokenFactory")]
    [InlineData(typeof(IDeviceLabels), "UserAgentDeviceLabels")]
    [InlineData(typeof(TimeProvider), "ClockTimeProvider")]
    public async Task Each_port_is_registered_with_its_adapter(Type port, string adapter)
    {
        await using ServiceProvider provider = fixture.BuildServices(
            DatabaseRole.Api,
            services => services.PostConfigure<JwtSettings>(settings =>
            {
                settings.SigningKey = new string('k', 64);
                settings.Issuer = "wattwise-api";
                settings.Audience = "wattwise-clients";
            }));
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        object instance = scope.ServiceProvider.GetRequiredService(port);

        Assert.Equal(adapter, instance.GetType().Name);
    }

    [Fact]
    public async Task The_Jwt_settings_are_read_from_the_Jwt_section_of_the_configuration()
    {
        Dictionary<string, string?> jwt = new()
        {
            ["Jwt:SigningKey"] = new string('s', 64),
            ["Jwt:Issuer"] = "the-issuer",
            ["Jwt:Audience"] = "the-audience",
        };
        await using ServiceProvider provider = fixture.BuildServices(
            DatabaseRole.Api,
            services => services.AddSingleton<IConfiguration>(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(fixture.Database.ConfigurationFor(DatabaseRole.Api))
                    .AddInMemoryCollection(jwt)
                    .Build()));

        JwtSettings settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        Assert.Equal((new string('s', 64), "the-issuer", "the-audience"), (settings.SigningKey, settings.Issuer, settings.Audience));
    }
}
