using System.Text.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;
using NodaTime.Testing;

using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>
/// Class fixture: one Api host on a database clone of its own, with a <see cref="FakeClock"/> as the host's
/// <see cref="IClock"/>. Rows are read through the <c>cli</c> role (the <c>api</c> role may only insert outbox rows), and
/// links are issued directly through <see cref="EmailLinkTokenProvider"/> with the same clock, so the Api tests never read emails.
/// </summary>
public sealed class AccountsApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private readonly DatabaseFixture database = new(postgres);
    private TestHostFactory<Program>? factory;
    private ServiceProvider? reader;
    private ServiceProvider? issuer;

    public FakeClock Clock { get; } = new(Instant.FromUtc(2026, 11, 11, 12, 0));

    public async ValueTask InitializeAsync()
    {
        await database.InitializeAsync();
        factory = new TestHostFactory<Program>(
            ApiTestSettings.For(database.Database),
            services => services.Replace(ServiceDescriptor.Singleton<IClock>(Clock)));
        reader = database.BuildServices(DatabaseRole.Cli);
        issuer = database.BuildServices(DatabaseRole.Api, services => services.Replace(ServiceDescriptor.Singleton<IClock>(Clock)));
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }

        if (reader is not null)
        {
            await reader.DisposeAsync();
        }

        if (issuer is not null)
        {
            await issuer.DisposeAsync();
        }

        await database.DisposeAsync();
    }

    public Task<HttpResponseMessage> PostAsync(string path, object body)
    {
        HttpClient client = factory!.CreateClient();
        return client.PostAsJsonAsync($"/api/v1/auth/{path}", body, TestContext.Current.CancellationToken);
    }

    /// <summary>A client of a host with <paramref name="configure"/> applied to its services, on the same database.</summary>
    public HttpClient CreateClientWith(Action<IServiceCollection> configure) =>
        factory!.WithWebHostBuilder(builder => builder.ConfigureTestServices(configure)).CreateClient();

    public Task<HttpResponseMessage> PostRawAsync(string path, HttpContent content)
    {
        HttpClient client = factory!.CreateClient();
        return client.PostAsync($"/api/v1/auth/{path}", content, TestContext.Current.CancellationToken);
    }

    /// <summary>Registers a new account through the endpoint and returns its ids; the email is as typed, normalized by the Api.</summary>
    public async Task<(Guid AccountId, Guid IdentityId)> RegisterAsync(string email)
    {
        using HttpResponseMessage response = await PostAsync(
            "register", new { email, password = "Aa1!" + Guid.NewGuid().ToString("N"), language = "lt" });
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        return (await AccountIdOfAsync(email), await IdentityIdOfAsync(email));
    }

    /// <summary>Rows of <c>app.email_requests</c> for an account, each as <c>kind|status|account_id|email</c> (NULL as nothing).</summary>
    public Task<string[]> OutboxRowsForAccountAsync(Guid accountId) =>
        QueryAsync($"""
            SELECT kind || '|' || status || '|' || coalesce(account_id::text, '') || '|' || coalesce(email, '') AS "Value"
            FROM app.email_requests WHERE account_id = {accountId}
            ORDER BY created_at, id
            """);

    /// <summary>Rows of <c>app.email_requests</c> for an address, in the same format.</summary>
    public Task<string[]> OutboxRowsForEmailAsync(string normalizedEmail) =>
        QueryAsync($"""
            SELECT kind || '|' || status || '|' || coalesce(account_id::text, '') || '|' || coalesce(email, '') AS "Value"
            FROM app.email_requests WHERE email = {normalizedEmail}
            ORDER BY created_at, id
            """);

    public async Task<Guid> AccountIdOfAsync(string email) =>
        Guid.Parse((await QueryAsync($"""
            SELECT account_id::text AS "Value" FROM app.identity_users WHERE normalized_email = {email.Trim().ToLowerInvariant()}
            """)).Single());

    public async Task<Guid> IdentityIdOfAsync(string email) =>
        Guid.Parse((await QueryAsync($"""
            SELECT id::text AS "Value" FROM app.identity_users WHERE normalized_email = {email.Trim().ToLowerInvariant()}
            """)).Single());

    /// <summary>The number of <c>identity_users</c> rows for an address.</summary>
    public async Task<int> IdentityCountAsync(string email) =>
        (await QueryAsync($"""
            SELECT id::text AS "Value" FROM app.identity_users WHERE normalized_email = {email.Trim().ToLowerInvariant()}
            """)).Length;

    public async Task<string> ConfirmedAndPreferredLanguageAsync(Guid identityId) =>
        (await QueryAsync($"""
            SELECT u.email_confirmed::text || '|' || a.preferred_language || '|' || u.normalized_email AS "Value"
            FROM app.identity_users u JOIN app.accounts a ON a.id = u.account_id WHERE u.id = {identityId}
            """)).Single();

    public async Task<bool> PasswordMatchesAsync(Guid identityId, string password)
    {
        await using AsyncServiceScope scope = issuer!.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = (await users.FindByIdAsync(identityId.ToString()))!;
        return await users.CheckPasswordAsync(identity, password);
    }

    /// <summary>Issues a confirmation link for the AccountIdentity the way the worker does, in a scope of its own.</summary>
    public async Task<string> IssueConfirmationTokenAsync(Guid identityId)
    {
        await using AsyncServiceScope scope = issuer!.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = (await users.FindByIdAsync(identityId.ToString()))!;
        return await users.GenerateEmailConfirmationTokenAsync(identity);
    }

    /// <summary>One row, one <c>"Value"</c> column, read through the <c>cli</c> role.</summary>
    public async Task<string> ScalarAsync(FormattableString sql) => (await QueryAsync(sql)).Single();

    /// <summary>
    /// The outbox rows of an account as <c>status|attempt_count|created_at = now|next_attempt_at = now|non-null keys</c>,
    /// where <c>now</c> is the fixture's clock.
    /// </summary>
    public Task<string[]> OutboxStateForAccountAsync(Guid accountId)
    {
        Instant now = Clock.GetCurrentInstant();
        return QueryAsync($"""
            SELECT status || '|' || attempt_count || '|' || (created_at = {now})::text || '|' || (next_attempt_at = {now})::text
                || '|' || num_nonnulls(account_id, email) AS "Value"
            FROM app.email_requests WHERE account_id = {accountId}
            """);
    }

    /// <summary>The same as <see cref="OutboxStateForAccountAsync"/> for the rows keyed by an address.</summary>
    public Task<string[]> OutboxStateForEmailAsync(string normalizedEmail)
    {
        Instant now = Clock.GetCurrentInstant();
        return QueryAsync($"""
            SELECT status || '|' || attempt_count || '|' || (created_at = {now})::text || '|' || (next_attempt_at = {now})::text
                || '|' || num_nonnulls(account_id, email) AS "Value"
            FROM app.email_requests WHERE email = {normalizedEmail}
            """);
    }

    /// <summary>Issues a password-reset link for the AccountIdentity, in a scope of its own.</summary>
    public async Task<string> IssuePasswordResetTokenAsync(Guid identityId)
    {
        await using AsyncServiceScope scope = issuer!.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = (await users.FindByIdAsync(identityId.ToString()))!;
        return await users.GeneratePasswordResetTokenAsync(identity);
    }

    /// <summary>Replaces the AccountIdentity's security stamp the way a password change does, but ends no session.</summary>
    public async Task RotateSecurityStampAsync(Guid identityId)
    {
        await using AsyncServiceScope scope = issuer!.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = (await users.FindByIdAsync(identityId.ToString()))!;
        Assert.True((await users.UpdateSecurityStampAsync(identity)).Succeeded);
    }

    /// <summary>The number of sessions of an account.</summary>
    public async Task<string> SessionCountAsync(Guid accountId) =>
        await ScalarAsync($"""SELECT count(*)::text AS "Value" FROM app.sessions WHERE account_id = {accountId}""");

    /// <summary>The number of reset-link rows of an AccountIdentity.</summary>
    public async Task<string> ResetLinkCountAsync(Guid identityId) =>
        await ScalarAsync($"""SELECT count(*)::text AS "Value" FROM app.email_link_tokens WHERE identity_id = {identityId} AND purpose = 'ResetPassword'""");

    /// <summary>A new account with an AccountIdentity (email confirmed unless <paramref name="confirmed"/> is false), made directly in the database.</summary>
    public Task<AccountSeeder.Ids> CreateAccountAsync(string email, string password, bool confirmed = true) =>
        AccountSeeder.CreateAccountAsync(issuer!, email, password, confirmed);

    /// <summary>Stores a hash of the password with 1000 iterations, which the host's options (100000) call old, so the next sign-in rehashes it.</summary>
    public async Task StoreOldPasswordHashAsync(Guid identityId, string password)
    {
        PasswordHasher<AccountIdentity> oldHasher = new(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
        await using AsyncServiceScope scope = issuer!.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = (await users.FindByIdAsync(identityId.ToString()))!;
        identity.PasswordHash = oldHasher.HashPassword(identity, password);
        Assert.True((await users.UpdateAsync(identity)).Succeeded);
    }

    /// <summary>Creates a role and a permission per name, and gives the role to the account.</summary>
    public Task GrantAsync(Guid accountId, string roleName, params string[] permissionNames) =>
        AccountSeeder.GrantAsync(issuer!, accountId, roleName, permissionNames);

    /// <summary>Signs in through the endpoint and returns the access token, its expiry text and the refresh cookie.</summary>
    public async Task<SignedInSession> SignInAsync(string email, string password, string? userAgent = null)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password }, userAgent: userAgent);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return new SignedInSession(
            body.RootElement.GetProperty("accessToken").GetString()!,
            body.RootElement.GetProperty("accessTokenExpiresAt").GetString()!,
            SetCookieHeader.RefreshCookieOf(response)!.NameAndValue);
    }

    /// <summary>One request on a client of its own; the cookie is sent as given (<c>name=value</c>), the token as a bearer token.</summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? cookie = null, string? bearer = null, object? body = null, string? userAgent = null)
    {
        HttpClient client = factory!.CreateClient();
        HttpRequestMessage request = new(method, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (bearer is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        }

        if (userAgent is not null)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The sessions of an account as <c>device|signed_in = now|last_used = now|tokens</c> per row, newest sign-in last.</summary>
    public Task<string[]> SessionRowsAsync(Guid accountId) =>
        QueryAsync($"""
            SELECT s.device_label || '|' || (s.signed_in_at = {Clock.GetCurrentInstant()})::text || '|' || (s.last_used_at = {Clock.GetCurrentInstant()})::text
                || '|' || (SELECT count(*) FROM app.refresh_tokens t WHERE t.session_id = s.id) AS "Value"
            FROM app.sessions s WHERE s.account_id = {accountId} ORDER BY s.signed_in_at, s.id
            """);

    /// <summary>The refresh tokens of an account as <c>used</c> or <c>unused</c>, in the order they were issued.</summary>
    public Task<string[]> RefreshTokenStatesAsync(Guid accountId) =>
        QueryAsync($"""
            SELECT CASE WHEN t.used_at IS NULL THEN 'unused' ELSE 'used' END AS "Value"
            FROM app.refresh_tokens t JOIN app.sessions s ON s.id = t.session_id
            WHERE s.account_id = {accountId} ORDER BY t.issued_at, t.used_at NULLS LAST
            """);

    private async Task<string[]> QueryAsync(FormattableString sql)
    {
        await using AsyncServiceScope scope = reader!.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<string>(sql).ToArrayAsync(TestContext.Current.CancellationToken);
    }
}
