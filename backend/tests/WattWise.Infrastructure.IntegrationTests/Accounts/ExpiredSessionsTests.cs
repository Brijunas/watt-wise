using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>
/// The purge acts on whole tables, so these tests have a clone of their own. Each purges as of <see cref="Now"/> and
/// asserts only on its own rows.
/// </summary>
public class ExpiredSessionsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0, 0);

    private static CancellationToken Token => AccountsTestSupport.Token;

    private static byte[] NewHash() => Guid.NewGuid().ToByteArray();

    private static async Task<Session> SeedAsync(ServiceProvider provider, Guid accountId, Instant expiresAt, Action<Session>? change = null)
    {
        Session session = Session.Start(accountId, "Firefox on Linux", NewHash(), Now - Duration.FromDays(30), expiresAt);
        change?.Invoke(session);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(session);
        await db.SaveChangesAsync(Token);
        return session;
    }

    private static async Task<int> PurgeAsync(ServiceProvider jobs)
    {
        await using AsyncServiceScope scope = jobs.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IExpiredSessions>().PurgeAsync(Now, Token);
    }

    private async Task<Guid> NewAccountAsync(ServiceProvider provider) =>
        (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;

    [Fact]
    public async Task A_session_whose_only_token_expires_after_now_is_kept_with_its_token()
    {
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Session session = await SeedAsync(api, await NewAccountAsync(api), Now + Duration.FromSeconds(1));

        await PurgeAsync(jobs);

        Assert.Equal("1|1", await SessionRows.OfSessionAsync(reader, session.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-86400)]
    public async Task A_session_whose_only_token_has_expired_by_now_is_deleted_with_its_token(int expiryOffsetSeconds)
    {
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Session session = await SeedAsync(api, await NewAccountAsync(api), Now + Duration.FromSeconds(expiryOffsetSeconds));

        await PurgeAsync(jobs);

        Assert.Equal("0|0", await SessionRows.OfSessionAsync(reader, session.Id));
    }

    [Fact]
    public async Task A_live_session_keeps_its_newest_token_and_loses_the_expired_one()
    {
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        byte[] firstHash = NewHash();
        Session session = Session.Start(await NewAccountAsync(api), "Firefox on Linux", firstHash, Now - Duration.FromDays(9), Now - Duration.FromDays(2));
        // The first token expired two days ago; the rotated one was issued a day ago and runs six more.
        byte[] liveHash = NewHash();
        session.Rotate(firstHash, liveHash, Now - Duration.FromDays(3) - Duration.FromSeconds(1), Now + Duration.FromDays(6));
        await using (AsyncServiceScope scope = api.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(session);
            await db.SaveChangesAsync(Token);
        }

        await PurgeAsync(jobs);

        Assert.Equal([Convert.ToHexString(liveHash)], await SessionRows.TokenHashesAsync(reader, session.Id));
    }

    [Fact]
    public async Task A_used_token_that_expires_exactly_now_is_deleted_while_the_live_token_keeps_the_session()
    {
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        byte[] firstHash = NewHash();
        Session session = Session.Start(await NewAccountAsync(api), "Firefox on Linux", firstHash, Now - Duration.FromDays(7), Now);
        byte[] liveHash = NewHash();
        session.Rotate(firstHash, liveHash, Now - Duration.FromDays(1), Now + Duration.FromDays(6));
        await using (AsyncServiceScope scope = api.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(session);
            await db.SaveChangesAsync(Token);
        }

        await PurgeAsync(jobs);

        Assert.Equal([Convert.ToHexString(liveHash)], await SessionRows.TokenHashesAsync(reader, session.Id));
    }

    [Fact]
    public async Task A_session_whose_tokens_are_all_expired_goes_even_when_one_of_them_is_not_used()
    {
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        byte[] firstHash = NewHash();
        Session session = Session.Start(await NewAccountAsync(api), "Firefox on Linux", firstHash, Now - Duration.FromDays(20), Now - Duration.FromDays(13));
        session.Rotate(firstHash, NewHash(), Now - Duration.FromDays(14), Now - Duration.FromDays(7));
        await using (AsyncServiceScope scope = api.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(session);
            await db.SaveChangesAsync(Token);
        }

        await PurgeAsync(jobs);

        Assert.Equal("0|0", await SessionRows.OfSessionAsync(reader, session.Id));
    }
}
