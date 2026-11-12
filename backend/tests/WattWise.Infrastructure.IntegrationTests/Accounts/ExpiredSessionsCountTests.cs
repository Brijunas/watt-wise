using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>The returned count is a whole-table figure, so this class has one test and a clone of its own.</summary>
public class ExpiredSessionsCountTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task The_purge_returns_the_number_of_sessions_it_deleted_not_the_tokens()
    {
        Instant now = Instant.FromUtc(2026, 11, 11, 12, 0, 0);
        await using ServiceProvider api = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider jobs = fixture.BuildServices(DatabaseRole.Jobs);
        Guid accountId = (await AccountSeeder.CreateAccountAsync(api, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;
        await using (AsyncServiceScope scope = api.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            byte[] hash = Guid.NewGuid().ToByteArray();
            Session twoTokens = Session.Start(accountId, "Firefox on Linux", "stamp", hash, now - Duration.FromDays(20), now - Duration.FromDays(13));
            twoTokens.Rotate(hash, Guid.NewGuid().ToByteArray(), now - Duration.FromDays(14), now - Duration.FromDays(7));
            db.Add(twoTokens);
            db.Add(Session.Start(accountId, "Chrome on Windows", "stamp", Guid.NewGuid().ToByteArray(), now - Duration.FromDays(8), now - Duration.FromDays(1)));
            db.Add(Session.Start(accountId, "Safari on macOS", "stamp", Guid.NewGuid().ToByteArray(), now - Duration.FromDays(1), now + Duration.FromDays(6)));
            await db.SaveChangesAsync(AccountsTestSupport.Token);
        }

        await using AsyncServiceScope purge = jobs.CreateAsyncScope();
        int deleted = await purge.ServiceProvider.GetRequiredService<IExpiredSessions>().PurgeAsync(now, AccountsTestSupport.Token);

        Assert.Equal(2, deleted);
    }
}
