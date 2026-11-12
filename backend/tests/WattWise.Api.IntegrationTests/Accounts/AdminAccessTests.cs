using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Api.Endpoints;
using WattWise.Domain.Accounts;

namespace WattWise.Api.IntegrationTests.Accounts;

public class AdminAccessTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private const string AdminPath = "/api/v1/admin/test";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private (HttpClient Client, ProbeCalls Calls) HostWithAdminEndpoint()
    {
        ProbeCalls calls = new();
        HttpClient client = fixture.CreateClientWith(services =>
        {
            services.AddSingleton(calls);
            services.AddSingleton<IAdminEndpointModule, TestAdminEndpointModule>();
        });
        return (client, calls);
    }

    private async Task<string> SignedInTokenAsync(bool admin)
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        if (admin)
        {
            await fixture.AssignRoleAsync(ids.AccountId, RoleNames.AdminId);
        }

        return (await fixture.SignInAsync(email, password)).AccessToken;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string? bearer)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, AdminPath);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request, Token);
    }

    [Fact]
    public async Task An_anonymous_call_to_an_admin_endpoint_answers_401_ProblemDetails()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();

        using HttpResponseMessage response = await GetAsync(client, null);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task An_anonymous_call_to_an_admin_endpoint_is_challenged_with_Bearer()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();

        using HttpResponseMessage response = await GetAsync(client, null);

        Assert.Equal("Bearer", string.Join(", ", response.Headers.WwwAuthenticate.Select(challenge => challenge.ToString())));
    }

    [Fact]
    public async Task A_signed_in_non_admin_gets_403_ProblemDetails_from_an_admin_endpoint()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();
        string token = await SignedInTokenAsync(admin: false);

        using HttpResponseMessage response = await GetAsync(client, token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "General.Forbidden", Token);
    }

    [Fact]
    public async Task A_signed_in_admin_gets_200_from_an_admin_endpoint()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();
        string token = await SignedInTokenAsync(admin: true);

        using HttpResponseMessage response = await GetAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_admin_call_reaches_the_admin_endpoint_after_an_anonymous_and_a_non_admin_call_were_refused()
    {
        (HttpClient client, ProbeCalls calls) = HostWithAdminEndpoint();
        string nonAdmin = await SignedInTokenAsync(admin: false);
        string admin = await SignedInTokenAsync(admin: true);
        (await GetAsync(client, null)).Dispose();
        (await GetAsync(client, nonAdmin)).Dispose();
        int refused = calls.Count;

        (await GetAsync(client, admin)).Dispose();

        Assert.Equal((0, 1), (refused, calls.Count));
    }

    [Fact]
    public async Task Me_of_an_admin_lists_the_Admin_role_and_the_admin_access_permission()
    {
        string token = await SignedInTokenAsync(admin: true);

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: token);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        string[] roles = [.. body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!)];
        string[] permissions = [.. body.RootElement.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()!)];
        Assert.Equal("Admin|admin.access", $"{string.Join(",", roles)}|{string.Join(",", permissions)}");
    }

    [Fact]
    public async Task After_a_revoke_the_old_refresh_cookie_is_refused_with_401_InvalidRefreshToken()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        await fixture.AssignRoleAsync(ids.AccountId, RoleNames.AdminId);
        SignedInSession session = await fixture.SignInAsync(email, password);
        await fixture.RevokeAdminAsync(email);

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: session.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task A_signed_in_account_whose_role_grants_another_permission_gets_403_from_an_admin_endpoint()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();
        string email = UniqueEmail();
        string password = ValidPassword();
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        await fixture.GrantAsync(ids.AccountId, $"{prefix}-editor", $"{prefix}.write");
        string token = (await fixture.SignInAsync(email, password)).AccessToken;

        using HttpResponseMessage response = await GetAsync(client, token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "General.Forbidden", Token);
    }

    [Fact]
    public async Task After_a_revoke_a_fresh_sign_in_gets_403_from_an_admin_endpoint()
    {
        (HttpClient client, _) = HostWithAdminEndpoint();
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        await fixture.AssignRoleAsync(ids.AccountId, RoleNames.AdminId);
        await fixture.RevokeAdminAsync(email);
        string token = (await fixture.SignInAsync(email, password)).AccessToken;

        using HttpResponseMessage response = await GetAsync(client, token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "General.Forbidden", Token);
    }
}
