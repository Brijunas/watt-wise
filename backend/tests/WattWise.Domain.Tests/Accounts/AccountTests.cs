using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Domain.Tests.Accounts;

public class AccountTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 2, 3, 4);

    [Fact]
    public void Create_sets_a_version_7_id_and_keeps_the_language_and_time()
    {
        Account account = Account.Create(Language.En, Now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(7, account.Id.Version);
        Assert.Equal(Language.En, account.PreferredLanguage);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Empty(account.RoleIds);
    }

    [Fact]
    public void AddRole_twice_with_the_same_role_adds_it_once()
    {
        Account account = Account.Create(Language.Lt, Now);
        Role role = Role.Create("Admin");

        account.AddRole(role.Id);
        account.AddRole(role.Id);

        Assert.Equal(role.Id, Assert.Single(account.RoleIds));
    }

}
