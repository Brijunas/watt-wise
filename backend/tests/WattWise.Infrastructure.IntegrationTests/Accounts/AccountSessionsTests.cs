using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountSessionsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant SignedInAt = Instant.FromUtc(2026, 11, 11, 12, 0);

    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<Guid> NewAccountAsync(ServiceProvider provider) =>
        (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;

    private static async Task<Session> AddSessionAsync(ServiceProvider provider, Guid accountId, bool rotated = false)
    {
        byte[] hash = Guid.NewGuid().ToByteArray();
        Session session = Session.Start(accountId, "Firefox on Linux", "stamp", hash, SignedInAt, SignedInAt + Duration.FromDays(7));
        if (rotated)
        {
            session.Rotate(hash, Guid.NewGuid().ToByteArray(), SignedInAt + Duration.FromMinutes(20), SignedInAt + Duration.FromDays(7));
        }

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISessionRepository>().Add(session);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        return session;
    }

    private static async Task<int> EndAllAsync(ServiceProvider provider, Guid accountId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountSessions>().EndAllAsync(accountId, Token);
    }

    private static async Task<int> EndAsync(ServiceProvider provider, Guid accountId, Guid sessionId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountSessions>().EndAsync(accountId, sessionId, Token);
    }

    [Fact]
    public async Task Ending_one_deletes_that_session_with_its_tokens_and_leaves_the_accounts_others()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        Session ended = await AddSessionAsync(provider, accountId, rotated: true);
        Session kept = await AddSessionAsync(provider, accountId);

        int count = await EndAsync(provider, accountId, ended.Id);

        Assert.Equal(
            (1, "0|0", "1|1"),
            (count, await SessionRows.OfSessionAsync(reader, ended.Id), await SessionRows.OfSessionAsync(reader, kept.Id)));
    }

    [Fact]
    public async Task Ending_another_accounts_session_deletes_nothing_and_returns_zero()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        Guid otherId = await NewAccountAsync(provider);
        Session others = await AddSessionAsync(provider, otherId);

        int count = await EndAsync(provider, accountId, others.Id);

        Assert.Equal((0, "1|1"), (count, await SessionRows.OfSessionAsync(reader, others.Id)));
    }

    [Fact]
    public async Task Ending_an_unknown_session_returns_zero()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);

        Assert.Equal(0, await EndAsync(provider, accountId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Ending_all_deletes_every_session_of_the_account_with_its_tokens_and_returns_how_many()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        Session first = await AddSessionAsync(provider, accountId);
        Session second = await AddSessionAsync(provider, accountId, rotated: true);

        int ended = await EndAllAsync(provider, accountId);

        Assert.Equal(
            (2, "0|0", "0|0"),
            (ended, await SessionRows.OfSessionAsync(reader, first.Id), await SessionRows.OfSessionAsync(reader, second.Id)));
    }

    [Fact]
    public async Task Ending_all_leaves_the_sessions_of_other_accounts()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        Guid otherId = await NewAccountAsync(provider);
        await AddSessionAsync(provider, accountId);
        Session others = await AddSessionAsync(provider, otherId);

        await EndAllAsync(provider, accountId);

        Assert.Equal("1|1", await SessionRows.OfSessionAsync(reader, others.Id));
    }

    [Fact]
    public async Task Ending_all_of_an_account_with_no_session_returns_zero()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);

        Assert.Equal(0, await EndAllAsync(provider, accountId));
    }
}
