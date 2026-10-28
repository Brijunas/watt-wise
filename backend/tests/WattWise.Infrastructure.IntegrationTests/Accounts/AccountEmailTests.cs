using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountEmailTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task The_stored_email_user_name_and_both_normalized_columns_hold_the_trimmed_NFKC_lowercase_form()
    {
        string unique = Guid.NewGuid().ToString("N");
        // U+FF2A full-width J, U+FF20 full-width @, U+FB01 "fi" ligature.
        string typed = $"  Ｊ-{unique}＠ﬁ.LT ";
        await using ServiceProvider provider = fixture.BuildAccountServices();

        AccountIdentityIds ids = await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, typed, AccountsTestSupport.ValidPassword(), Token);

        string expected = $"j-{unique}@fi.lt";
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        EmailRow stored = await db.Database
            .SqlQuery<EmailRow>($"""
                SELECT email, user_name, normalized_email, normalized_user_name
                FROM app.identity_users WHERE id = {ids.IdentityId}
                """)
            .SingleAsync(Token);
        Assert.Equal(new EmailRow(expected, expected, expected, expected), stored);
    }

    [Fact]
    public async Task An_email_differing_only_in_case_and_spaces_is_rejected_as_a_duplicate()
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        Guid secondAccountId = await AccountsTestSupport.SaveAccountAsync(provider, Token);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();

        IdentityResult result = await users.CreateAsync(
            AccountIdentity.Create(secondAccountId, "  " + email.ToUpperInvariant() + " "), AccountsTestSupport.ValidPassword());

        // The user name is the email, so ASP.NET Core Identity reports both.
        Assert.Equal(["DuplicateEmail", "DuplicateUserName"], EmailLinks.Codes(result).Order());
    }

    [Fact]
    public async Task The_database_rejects_a_duplicate_normalized_email_that_bypasses_the_user_manager()
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account other = AccountsTestSupport.NewAccount();
        db.Accounts.Add(other);
        AccountIdentity duplicate = AccountIdentity.Create(other.Id, email);
        duplicate.UserName = AccountsTestSupport.UniqueEmail(); // Only the email collides.
        duplicate.NormalizedUserName = duplicate.UserName;
        duplicate.NormalizedEmail = email;
        db.Users.Add(duplicate);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));

        Assert.Equal("23505:ix_identity_users_normalized_email", AccountsTestSupport.ViolationOf(exception));
    }
}
