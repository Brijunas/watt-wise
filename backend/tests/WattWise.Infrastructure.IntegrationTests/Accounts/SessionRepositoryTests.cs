using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class SessionRepositoryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Whole milliseconds: timestamptz keeps microseconds, so the values round trip exactly.
    private static readonly Instant SignedInAt = Instant.FromUtc(2026, 11, 11, 12, 0, 0) + Duration.FromMilliseconds(123);
    private static readonly Instant ExpiresAt = SignedInAt + Duration.FromDays(7);

    private static CancellationToken Token => AccountsTestSupport.Token;

    private static byte[] NewHash() => Guid.NewGuid().ToByteArray();

    private async Task<Guid> NewAccountAsync(ServiceProvider provider) =>
        (await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword())).AccountId;

    private static async Task AddAsync(ServiceProvider provider, Session session)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISessionRepository>().Add(session);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
    }

    // Microsecond precision, which timestamptz keeps.
    private static string At(Instant instant) => instant.ToString("uuuu'-'MM'-'dd'T'HH':'mm':'ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<string?> DescribeByHashAsync(ServiceProvider provider, byte[] hash)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Session? session = await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetByRefreshTokenHashAsync(hash, Token);
        return session is null
            ? null
            : $"{session.Id}|{session.AccountId}|{session.DeviceLabel}|{At(session.SignedInAt)}|{At(session.LastUsedAt)}|"
                + string.Join(",", session.RefreshTokens
                    .OrderBy(token => token.IssuedAt)
                    .Select(token => $"{Convert.ToHexString(token.TokenHash)}@{At(token.IssuedAt)}~{At(token.ExpiresAt)}!{(token.UsedAt is { } used ? At(used) : "unused")}"));
    }

    [Fact]
    public async Task An_added_session_is_found_by_its_token_hash_with_everything_it_stored()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);
        byte[] hash = NewHash();
        Session session = Session.Start(accountId, "Firefox on Linux", hash, SignedInAt, ExpiresAt);
        await AddAsync(provider, session);

        string? found = await DescribeByHashAsync(provider, hash);

        Assert.Equal(
            $"{session.Id}|{accountId}|Firefox on Linux|2026-11-11T12:00:00.123000Z|2026-11-11T12:00:00.123000Z|"
                + $"{Convert.ToHexString(hash)}@2026-11-11T12:00:00.123000Z~2026-11-18T12:00:00.123000Z!unused",
            found);
    }

    [Fact]
    public async Task An_unknown_hash_finds_nothing()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();

        Assert.Null(await DescribeByHashAsync(provider, NewHash()));
    }

    [Fact]
    public async Task A_hash_finds_its_own_session_and_not_another_one()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);
        byte[] firstHash = NewHash();
        byte[] secondHash = NewHash();
        Session first = Session.Start(accountId, "Firefox on Linux", firstHash, SignedInAt, ExpiresAt);
        Session second = Session.Start(accountId, "Chrome on Windows", secondHash, SignedInAt, ExpiresAt);
        await AddAsync(provider, first);
        await AddAsync(provider, second);

        string? found = await DescribeByHashAsync(provider, secondHash);

        Assert.Equal(second.Id.ToString(), found!.Split('|')[0]);
    }

    [Fact]
    public async Task The_hash_of_a_used_token_still_finds_the_session_with_both_tokens()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);
        byte[] oldHash = NewHash();
        byte[] newHash = NewHash();
        Session session = Session.Start(accountId, "Firefox on Linux", oldHash, SignedInAt, ExpiresAt);
        Instant later = SignedInAt + Duration.FromMinutes(20);
        session.Rotate(oldHash, newHash, later, later + Duration.FromDays(7));
        await AddAsync(provider, session);

        string? found = await DescribeByHashAsync(provider, oldHash);

        Assert.Equal(
            $"{session.Id}|{accountId}|Firefox on Linux|2026-11-11T12:00:00.123000Z|2026-11-11T12:20:00.123000Z|"
                + $"{Convert.ToHexString(oldHash)}@2026-11-11T12:00:00.123000Z~2026-11-18T12:00:00.123000Z!2026-11-11T12:20:00.123000Z,"
                + $"{Convert.ToHexString(newHash)}@2026-11-11T12:20:00.123000Z~2026-11-18T12:20:00.123000Z!unused",
            found);
    }

    [Fact]
    public async Task Rotating_a_stored_session_persists_the_used_token_the_new_token_and_the_last_use()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);
        byte[] oldHash = NewHash();
        byte[] newHash = NewHash();
        Session session = Session.Start(accountId, "Firefox on Linux", oldHash, SignedInAt, ExpiresAt);
        await AddAsync(provider, session);
        Instant later = SignedInAt + Duration.FromMinutes(20);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            Session loaded = (await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetByRefreshTokenHashAsync(oldHash, Token))!;
            loaded.Rotate(oldHash, newHash, later, later + Duration.FromDays(7));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        string? found = await DescribeByHashAsync(provider, newHash);

        Assert.Equal(
            $"{session.Id}|{accountId}|Firefox on Linux|2026-11-11T12:00:00.123000Z|2026-11-11T12:20:00.123000Z|"
                + $"{Convert.ToHexString(oldHash)}@2026-11-11T12:00:00.123000Z~2026-11-18T12:00:00.123000Z!2026-11-11T12:20:00.123000Z,"
                + $"{Convert.ToHexString(newHash)}@2026-11-11T12:20:00.123000Z~2026-11-18T12:20:00.123000Z!unused",
            found);
    }

    [Fact]
    public async Task Removing_a_session_deletes_it_and_its_tokens_and_leaves_the_others()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        byte[] hash = NewHash();
        Session removed = Session.Start(accountId, "Firefox on Linux", hash, SignedInAt, ExpiresAt);
        Session kept = Session.Start(accountId, "Chrome on Windows", NewHash(), SignedInAt, ExpiresAt);
        await AddAsync(provider, removed);
        await AddAsync(provider, kept);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ISessionRepository repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            repository.Remove((await repository.GetByRefreshTokenHashAsync(hash, Token))!);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        Assert.Equal(
            ("0|0", "1|1"),
            (await SessionRows.OfSessionAsync(reader, removed.Id), await SessionRows.OfSessionAsync(reader, kept.Id)));
    }

    [Fact]
    public async Task Removing_a_session_found_by_the_hash_of_its_used_token_deletes_both_tokens()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        byte[] oldHash = NewHash();
        Session session = Session.Start(accountId, "Firefox on Linux", oldHash, SignedInAt, ExpiresAt);
        session.Rotate(oldHash, NewHash(), SignedInAt + Duration.FromMinutes(20), ExpiresAt);
        await AddAsync(provider, session);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ISessionRepository repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            repository.Remove((await repository.GetByRefreshTokenHashAsync(oldHash, Token))!);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        Assert.Equal("0|0", await SessionRows.OfSessionAsync(reader, session.Id));
    }

    [Fact]
    public async Task Two_sessions_with_the_same_token_hash_violate_the_unique_index()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Guid accountId = await NewAccountAsync(provider);
        byte[] hash = NewHash();
        await AddAsync(provider, Session.Start(accountId, "Firefox on Linux", hash, SignedInAt, ExpiresAt));

        Exception? thrown = await Record.ExceptionAsync(
            () => AddAsync(provider, Session.Start(accountId, "Chrome on Windows", hash, SignedInAt, ExpiresAt)));

        Assert.Equal("23505:ix_refresh_tokens_token_hash", AccountsTestSupport.ViolationOf(thrown));
    }

    [Fact]
    public async Task A_session_of_an_account_that_does_not_exist_violates_the_foreign_key()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();

        Exception? thrown = await Record.ExceptionAsync(
            () => AddAsync(provider, Session.Start(Guid.NewGuid(), "Firefox on Linux", NewHash(), SignedInAt, ExpiresAt)));

        Assert.Equal("23503:fk_sessions_accounts_account_id", ViolationOf(thrown));
    }

    [Fact]
    public async Task Deleting_the_account_deletes_its_sessions_and_tokens()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId = await NewAccountAsync(provider);
        Session session = Session.Start(accountId, "Firefox on Linux", NewHash(), SignedInAt, ExpiresAt);
        await AddAsync(provider, session);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Accounts.Where(account => account.Id == accountId).ExecuteDeleteAsync(Token);
        }

        Assert.Equal("0|0", await SessionRows.OfSessionAsync(reader, session.Id));
    }

    private static string ViolationOf(Exception? exception) => AccountsTestSupport.ViolationOf(exception);
}
