using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountsQueriesSessionsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<Guid> NewAccountAsync(ServiceProvider provider) =>
        (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;

    private static async Task<IReadOnlyList<SessionListItem>> SessionsAsync(ServiceProvider provider, Guid accountId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountsQueries>().GetSessionsAsync(accountId, Now, Token);
    }

    [Fact]
    public async Task A_session_whose_only_token_expires_exactly_now_is_not_listed_and_one_second_later_it_is()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);
        Instant signedIn = Now - Duration.FromDays(1);
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Expires now", signedIn, Now));
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Expires in a second", signedIn, Now + Duration.FromSeconds(1)));

        IReadOnlyList<SessionListItem> sessions = await SessionsAsync(provider, accountId);

        Assert.Equal(["Expires in a second"], sessions.Select(session => session.DeviceLabel));
    }

    [Fact]
    public async Task A_rotated_session_is_listed_through_its_newer_token_with_its_stored_times()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);
        Instant signedIn = Instant.FromUtc(2026, 11, 5, 8, 15);
        Instant rotated = Instant.FromUtc(2026, 11, 11, 11, 59);
        Session session = SessionSeeder.StartRotated(
            accountId, "Firefox on Linux", signedIn, Now - Duration.FromSeconds(1), rotated, rotated + Duration.FromDays(7));
        await SessionSeeder.AddAsync(provider, session);

        IReadOnlyList<SessionListItem> sessions = await SessionsAsync(provider, accountId);

        Assert.Equal([new SessionListItem(session.Id, "Firefox on Linux", signedIn, rotated)], sessions);
    }

    [Fact]
    public async Task Sessions_come_most_recently_used_first()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);
        Instant expires = Now + Duration.FromDays(7);
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Used second", Now - Duration.FromHours(5), expires));
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Used first", Now - Duration.FromHours(1), expires));
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Used third", Now - Duration.FromHours(9), expires));

        IReadOnlyList<SessionListItem> sessions = await SessionsAsync(provider, accountId);

        Assert.Equal(["Used first", "Used second", "Used third"], sessions.Select(session => session.DeviceLabel));
    }

    [Fact]
    public async Task Sessions_used_at_the_same_instant_come_in_id_order()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);
        Instant expires = Now + Duration.FromDays(7);
        List<Session> started = [.. Enumerable.Range(0, 4).Select(number => SessionSeeder.Start(accountId, $"Device {number}", Now, expires))];
        string[] expected = [.. started.Select(session => session.Id.ToString()).Order(StringComparer.Ordinal)];
        // Stored in descending id order, so an unordered query would not return them ascending.
        foreach (Session session in started.OrderByDescending(session => session.Id.ToString(), StringComparer.Ordinal))
        {
            await SessionSeeder.AddAsync(provider, session);
        }

        IReadOnlyList<SessionListItem> sessions = await SessionsAsync(provider, accountId);

        Assert.Equal(expected, sessions.Select(session => session.Id.ToString()));
    }

    [Fact]
    public async Task Another_accounts_sessions_are_not_listed()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);
        Guid otherId = await NewAccountAsync(provider);
        Instant expires = Now + Duration.FromDays(7);
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(accountId, "Mine", Now, expires));
        await SessionSeeder.AddAsync(provider, SessionSeeder.Start(otherId, "Theirs", Now, expires));

        IReadOnlyList<SessionListItem> sessions = await SessionsAsync(provider, accountId);

        Assert.Equal(["Mine"], sessions.Select(session => session.DeviceLabel));
    }

    [Fact]
    public async Task An_account_with_no_session_gets_an_empty_list()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await NewAccountAsync(provider);

        Assert.Empty(await SessionsAsync(provider, accountId));
    }
}
