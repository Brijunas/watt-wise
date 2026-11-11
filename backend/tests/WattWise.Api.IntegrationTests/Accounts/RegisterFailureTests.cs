using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using WattWise.Application.Email;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Has a database of its own, so the tests may count whole tables.</summary>
public class RegisterFailureTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    [Fact]
    public async Task A_failure_while_saving_the_outbox_row_persists_no_account_no_identity_and_no_request()
    {
        using HttpClient client = fixture.CreateClientWith(services =>
        {
            ServiceDescriptor real = services.Single(d => d.ServiceType == typeof(IEmailRequests));
            services.Replace(ServiceDescriptor.Describe(
                typeof(IEmailRequests),
                provider => new RequestForAMissingAccount((IEmailRequests)ActivatorUtilities.CreateInstance(provider, real.ImplementationType!)),
                real.Lifetime));
        });

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = $"Aa1!{Guid.NewGuid():N}", language = "lt" },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.InternalServerError, "0|0|0"),
            (response.StatusCode, await fixture.ScalarAsync($"""
                SELECT (SELECT count(*) FROM app.accounts)::text || '|' || (SELECT count(*) FROM app.identity_users)::text
                    || '|' || (SELECT count(*) FROM app.email_requests)::text AS "Value"
                """)));
    }

    /// <summary>Writes the request for an account that does not exist, so saving fails on the foreign key after the account is staged.</summary>
    private sealed class RequestForAMissingAccount(IEmailRequests inner) : IEmailRequests
    {
        public void Add(EmailRequestKind kind, Guid accountId) => inner.Add(kind, Guid.NewGuid());

        public void Add(EmailRequestKind kind, string normalizedEmail) => inner.Add(kind, normalizedEmail);
    }
}
