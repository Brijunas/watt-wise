using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountsQueriesTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<IReadOnlyList<string>> PermissionsAsync(ServiceProvider provider, Guid accountId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountsQueries>().GetPermissionsAsync(accountId, Token);
    }

    private static async Task<Me?> MeAsync(ServiceProvider provider, Guid accountId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountsQueries>().GetMeAsync(accountId, Token);
    }

    private static Task<AccountSeeder.Ids> NewAccountAsync(ServiceProvider provider, string? email = null, Language language = Language.Lt) =>
        AccountSeeder.CreateAccountAsync(provider, email ?? AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), language: language);

    [Fact]
    public async Task Permissions_are_the_distinct_names_across_all_the_accounts_roles_in_ordinal_order()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await NewAccountAsync(provider);
        Guid shared = await AccountSeeder.CreatePermissionAsync(provider, $"{prefix}.b-shared");
        Guid first = await AccountSeeder.CreatePermissionAsync(provider, $"{prefix}.c-first");
        Guid second = await AccountSeeder.CreatePermissionAsync(provider, $"{prefix}.a-second");
        await AccountSeeder.AssignRoleAsync(provider, ids.AccountId, await AccountSeeder.CreateRoleAsync(provider, $"{prefix}-one", shared, first));
        await AccountSeeder.AssignRoleAsync(provider, ids.AccountId, await AccountSeeder.CreateRoleAsync(provider, $"{prefix}-two", shared, second));

        IReadOnlyList<string> permissions = await PermissionsAsync(provider, ids.AccountId);

        Assert.Equal([$"{prefix}.a-second", $"{prefix}.b-shared", $"{prefix}.c-first"], permissions);
    }

    [Fact]
    public async Task Twelve_permissions_created_in_reverse_order_come_back_in_ordinal_order()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await NewAccountAsync(provider);
        List<Guid> permissionIds = [];
        for (int number = 12; number >= 1; number--)
        {
            permissionIds.Add(await AccountSeeder.CreatePermissionAsync(provider, $"{prefix}.p{number:00}"));
        }

        await AccountSeeder.AssignRoleAsync(provider, ids.AccountId, await AccountSeeder.CreateRoleAsync(provider, $"{prefix}-role", [.. permissionIds]));

        Assert.Equal(
            string.Join(',', Enumerable.Range(1, 12).Select(number => $"{prefix}.p{number:00}")),
            string.Join(',', await PermissionsAsync(provider, ids.AccountId)));
    }

    [Fact]
    public async Task An_account_without_roles_has_no_permissions()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        AccountSeeder.Ids ids = await NewAccountAsync(provider);

        Assert.Empty(await PermissionsAsync(provider, ids.AccountId));
    }

    [Fact]
    public async Task Another_accounts_roles_do_not_leak_into_the_permissions()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids mine = await NewAccountAsync(provider);
        AccountSeeder.Ids other = await NewAccountAsync(provider);
        await AccountSeeder.GrantAsync(provider, other.AccountId, $"{prefix}-role", $"{prefix}.other-only");
        await AccountSeeder.GrantAsync(provider, mine.AccountId, $"{prefix}-mine", $"{prefix}.mine");

        IReadOnlyList<string> permissions = await PermissionsAsync(provider, mine.AccountId);

        Assert.Equal([$"{prefix}.mine"], permissions);
    }

    [Fact]
    public async Task A_role_without_permissions_gives_none()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        AccountSeeder.Ids ids = await NewAccountAsync(provider);
        await AccountSeeder.AssignRoleAsync(provider, ids.AccountId, await AccountSeeder.CreateRoleAsync(provider, AccountsTestSupport.UniqueName("empty")));

        Assert.Empty(await PermissionsAsync(provider, ids.AccountId));
    }

    [Fact]
    public async Task Me_has_the_email_the_language_the_sorted_roles_and_the_permissions()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        string prefix = Guid.NewGuid().ToString("N");
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(provider, email, Language.En);
        await AccountSeeder.GrantAsync(provider, ids.AccountId, $"{prefix}-viewer", $"{prefix}.read");
        await AccountSeeder.GrantAsync(provider, ids.AccountId, $"{prefix}-admin", $"{prefix}.write");

        Me? me = await MeAsync(provider, ids.AccountId);

        Assert.Equal(
            new Me(email, "en", [$"{prefix}-admin", $"{prefix}-viewer"], [$"{prefix}.read", $"{prefix}.write"]),
            me,
            new MeComparer());
    }

    [Theory]
    [InlineData(Language.Lt, "lt")]
    [InlineData(Language.En, "en")]
    public async Task Me_names_the_preferred_language_in_lowercase(Language language, string expected)
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        AccountSeeder.Ids ids = await NewAccountAsync(provider, language: language);

        Me? me = await MeAsync(provider, ids.AccountId);

        Assert.Equal(expected, me!.Language);
    }

    [Fact]
    public async Task Me_of_an_account_without_roles_has_empty_lists()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(provider, email);

        Me? me = await MeAsync(provider, ids.AccountId);

        Assert.Equal(new Me(email, "lt", [], []), me, new MeComparer());
    }

    [Fact]
    public async Task Me_of_an_unknown_account_is_null()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);

        Assert.Null(await MeAsync(provider, Guid.NewGuid()));
    }

    // Me holds lists, which a record compares by reference.
    private sealed class MeComparer : IEqualityComparer<Me?>
    {
        public bool Equals(Me? x, Me? y) =>
            x is not null && y is not null
            && x.Email == y.Email && x.Language == y.Language && x.Roles.SequenceEqual(y.Roles) && x.Permissions.SequenceEqual(y.Permissions);

        public int GetHashCode(Me? obj) => obj?.Email.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
