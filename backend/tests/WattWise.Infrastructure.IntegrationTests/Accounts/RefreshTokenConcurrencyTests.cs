using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class RefreshTokenConcurrencyTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant SignedInAt = Instant.FromUtc(2026, 11, 11, 12, 0, 0);

    private static CancellationToken Token => AccountsTestSupport.Token;

    // Both scopes load the session and rotate the same token before either saves: the barrier holds each
    // SaveChanges until both have arrived, so the second save finds the token row already changed.
    [Fact]
    public async Task Two_exchanges_of_the_same_token_at_once_let_exactly_one_win_and_the_other_get_a_ConcurrencyConflict()
    {
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        Guid accountId = (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;
        byte[] hash = Guid.NewGuid().ToByteArray();
        await using (AsyncServiceScope seed = provider.CreateAsyncScope())
        {
            seed.ServiceProvider.GetRequiredService<ISessionRepository>().Add(Session.Start(accountId, "Firefox on Linux", "stamp", hash, SignedInAt, SignedInAt + Duration.FromDays(7)));
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        barrier.Arm(2);
        string[] outcomes = await Task.WhenAll(ExchangeAsync(provider, hash), ExchangeAsync(provider, hash));

        Assert.Equal("Conflict,Saved", string.Join(",", outcomes.Order()));
    }

    [Fact]
    public async Task After_a_lost_race_the_stored_session_has_exactly_one_new_token()
    {
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;
        byte[] hash = Guid.NewGuid().ToByteArray();
        Session session = Session.Start(accountId, "Firefox on Linux", "stamp", hash, SignedInAt, SignedInAt + Duration.FromDays(7));
        await using (AsyncServiceScope seed = provider.CreateAsyncScope())
        {
            seed.ServiceProvider.GetRequiredService<ISessionRepository>().Add(session);
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        barrier.Arm(2);
        await Task.WhenAll(ExchangeAsync(provider, hash), ExchangeAsync(provider, hash));

        Assert.Equal("1|2", await SessionRows.OfSessionAsync(reader, session.Id));
    }

    [Fact]
    public async Task A_lost_race_leaves_the_losers_change_tracker_empty_so_the_handler_can_load_fresh_state()
    {
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        Guid accountId = (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;
        byte[] hash = Guid.NewGuid().ToByteArray();
        await using (AsyncServiceScope seed = provider.CreateAsyncScope())
        {
            seed.ServiceProvider.GetRequiredService<ISessionRepository>().Add(Session.Start(accountId, "Firefox on Linux", "stamp", hash, SignedInAt, SignedInAt + Duration.FromDays(7)));
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        barrier.Arm(2);
        string[] results = await Task.WhenAll(TrackedAfterExchangeAsync(provider, hash), TrackedAfterExchangeAsync(provider, hash));

        Assert.Equal("Conflict:0", Assert.Single(results, result => result.StartsWith("Conflict", StringComparison.Ordinal)));
    }

    private static async Task<string> ExchangeAsync(ServiceProvider provider, byte[] hash)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Session session = (await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetByRefreshTokenHashAsync(hash, Token))!;
        session.Rotate(hash, Guid.NewGuid().ToByteArray(), SignedInAt + Duration.FromMinutes(5), SignedInAt + Duration.FromDays(7));
        try
        {
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
            return "Saved";
        }
        catch (ConcurrencyConflictException)
        {
            return "Conflict";
        }
    }

    private static async Task<string> TrackedAfterExchangeAsync(ServiceProvider provider, byte[] hash)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Session session = (await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetByRefreshTokenHashAsync(hash, Token))!;
        session.Rotate(hash, Guid.NewGuid().ToByteArray(), SignedInAt + Duration.FromMinutes(5), SignedInAt + Duration.FromDays(7));
        string outcome = "Saved";
        try
        {
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }
        catch (ConcurrencyConflictException)
        {
            outcome = "Conflict";
        }

        return $"{outcome}:{scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangeTracker.Entries().Count()}";
    }
}
