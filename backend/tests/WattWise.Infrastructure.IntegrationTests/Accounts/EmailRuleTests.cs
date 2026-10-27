using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class EmailRuleTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_email_differing_only_in_case_and_spaces_is_rejected_by_the_user_manager()
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account second = AccountsTestSupport.NewAccount();
        db.Accounts.Add(second);
        await db.SaveChangesAsync(Token);
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();

        IdentityResult result = await users.CreateAsync(
            AccountIdentity.Create(second.Id, "  " + email.ToUpperInvariant() + " "), AccountsTestSupport.ValidPassword());

        Assert.False(result.Succeeded);
        Assert.Contains("DuplicateEmail", result.Errors.Select(error => error.Code));
    }

    [Fact]
    public async Task The_stored_email_is_trimmed_and_lowercase()
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AccountIdentityIds ids = await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, "  " + email.ToUpperInvariant() + " ", AccountsTestSupport.ValidPassword(), Token);
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string stored = await db.Database
            .SqlQuery<string>($"SELECT email AS \"Value\" FROM app.identity_users WHERE id = {ids.IdentityId}")
            .SingleAsync(Token);
        string storedNormalized = await db.Database
            .SqlQuery<string>($"SELECT normalized_email AS \"Value\" FROM app.identity_users WHERE id = {ids.IdentityId}")
            .SingleAsync(Token);

        Assert.Equal(email, stored);
        Assert.Equal(email, storedNormalized);
    }

    [Fact]
    public async Task The_database_rejects_a_duplicate_normalized_email_that_bypasses_the_user_manager()
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account other = AccountsTestSupport.NewAccount();
        db.Accounts.Add(other);
        AccountIdentity duplicate = AccountIdentity.Create(other.Id, email);
        duplicate.UserName = AccountsTestSupport.UniqueEmail(); // Only the email collides.
        duplicate.NormalizedUserName = duplicate.UserName;
        duplicate.NormalizedEmail = email;
        db.Users.Add(duplicate);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));

        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(AccountsTestSupport.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_identity_users_normalized_email", postgres.ConstraintName);
    }
}
