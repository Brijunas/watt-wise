using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>A sign-in whose stored hash is old stores a current one without rotating the security stamp.</summary>
public class IdentityPasswordRehashTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private sealed record Seeded(
        ServiceProvider Provider, AccountSeeder.Ids Ids, string Email, string Password, string OldHash, string Stamp, RunBeforeCommandInterceptor Interceptor)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    /// <summary>An account whose stored hash is a V3 hash of its password with 1000 iterations, which the current options (100000) call old.</summary>
    private async Task<Seeded> SeedWithOldHashAsync()
    {
        RunBeforeCommandInterceptor interceptor = new();
        ServiceProvider provider = fixture.BuildAccountServices(
            new FakeClock(Instant.FromUtc(2026, 11, 11, 12, 0)),
            services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor)));
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(provider, email, password);
        PasswordHasher<AccountIdentity> oldHasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
        string oldHash = await AccountsTestSupport.InScopeAsync(provider, ids.IdentityId, async (users, identity) =>
        {
            identity.PasswordHash = oldHasher.HashPassword(identity, password);
            Assert.True((await users.UpdateAsync(identity)).Succeeded);
            return identity.PasswordHash;
        });
        string stamp = await AccountsTestSupport.InScopeAsync(provider, ids.IdentityId, (users, identity) => users.GetSecurityStampAsync(identity))!;
        return new Seeded(provider, ids, email, password, oldHash, stamp, interceptor);
    }

    private static async Task<PasswordCheck> SignInAsync(Seeded account)
    {
        await using AsyncServiceScope scope = account.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync(account.Email, account.Password, Token);
    }

    private static Task<string> StoredHashAsync(Seeded account) =>
        AccountsTestSupport.InScopeAsync(account.Provider, account.Ids.IdentityId, (_, identity) => Task.FromResult(identity.PasswordHash!));

    [Fact]
    public async Task A_sign_in_on_an_old_hash_succeeds_and_stores_a_current_hash()
    {
        await using Seeded account = await SeedWithOldHashAsync();

        PasswordCheck result = await SignInAsync(account);

        string stored = await StoredHashAsync(account);
        await using AsyncServiceScope scope = account.Provider.CreateAsyncScope();
        IPasswordHasher<AccountIdentity> hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountIdentity>>();
        Assert.Equal(
            (PasswordCheck.Succeeded(account.Ids.AccountId, account.Stamp), PasswordVerificationResult.Success, true),
            (result, hasher.VerifyHashedPassword(new AccountIdentity(), stored, account.Password), stored != account.OldHash));
    }

    [Fact]
    public async Task A_rehash_leaves_the_security_stamp_alone()
    {
        await using Seeded account = await SeedWithOldHashAsync();

        await SignInAsync(account);

        Assert.Equal(
            account.Stamp,
            await AccountsTestSupport.InScopeAsync(account.Provider, account.Ids.IdentityId, (users, identity) => users.GetSecurityStampAsync(identity)));
    }

    [Fact]
    public async Task The_password_still_verifies_after_a_rehash_and_a_wrong_one_does_not()
    {
        await using Seeded account = await SeedWithOldHashAsync();
        await SignInAsync(account);

        Assert.Equal(
            (true, false),
            (await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password),
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, "Wrong-password1!")));
    }

    [Fact]
    public async Task A_second_sign_in_after_a_rehash_changes_nothing_in_the_stored_hash()
    {
        await using Seeded account = await SeedWithOldHashAsync();
        await SignInAsync(account);
        string afterFirst = await StoredHashAsync(account);

        await SignInAsync(account);

        Assert.Equal(afterFirst, await StoredHashAsync(account));
    }

    [Fact]
    public async Task A_hash_changed_between_the_verify_and_the_rehash_is_kept()
    {
        await using Seeded account = await SeedWithOldHashAsync();
        string newPassword = AccountsTestSupport.ValidPassword();
        string newerHash = string.Empty;
        account.Interceptor.Arm("password_hash", async () =>
        {
            // A password change that commits after the sign-in verified the old hash.
            newerHash = await AccountsTestSupport.InScopeAsync(account.Provider, account.Ids.IdentityId, async (users, identity) =>
            {
                string hash = users.PasswordHasher.HashPassword(identity, newPassword);
                await using AsyncServiceScope scope = account.Provider.CreateAsyncScope();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                _ = await db.Users.Where(user => user.Id == identity.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(user => user.PasswordHash, hash), Token);
                return hash;
            });
        });

        await SignInAsync(account);

        Assert.Equal(
            (newerHash, true, false),
            (await StoredHashAsync(account),
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, newPassword),
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password)));
    }
}
