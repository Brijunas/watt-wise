using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

using WattWise.Api.Cors;

using AspNetCorsPolicy = Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy;

namespace WattWise.Api.Tests.Cors;

public class ConfigureCorsOptionsTests
{
    private static AspNetCorsPolicy PolicyOf(string name)
    {
        CorsOptions options = new();
        new ConfigureCorsOptions(Options.Create(new CorsSettings { AllowedOrigins = ["https://a.example.test", "https://b.example.test"] })).Configure(options);
        return options.GetPolicy(name)!;
    }

    [Theory]
    [InlineData("WattWiseClients", false)]
    [InlineData("WattWiseClientsWithCredentials", true)]
    public void Each_policy_has_the_configured_origins_methods_headers_and_max_age_and_only_the_auth_policy_allows_credentials(string name, bool credentials)
    {
        AspNetCorsPolicy policy = PolicyOf(name);

        Assert.Equal(
            ("https://a.example.test,https://b.example.test", "GET,POST,PUT,PATCH,DELETE", "Authorization,Content-Type", 600.0, credentials, false),
            (
                string.Join(",", policy.Origins),
                string.Join(",", policy.Methods),
                string.Join(",", policy.Headers),
                policy.PreflightMaxAge!.Value.TotalSeconds,
                policy.SupportsCredentials,
                policy.AllowAnyOrigin));
    }

    [Fact]
    public void The_policy_names_are_the_ones_the_endpoints_use()
    {
        Assert.Equal(("WattWiseClients", "WattWiseClientsWithCredentials"), (WattWise.Api.Cors.CorsPolicy.Name, AuthCorsPolicy.Name));
    }
}
