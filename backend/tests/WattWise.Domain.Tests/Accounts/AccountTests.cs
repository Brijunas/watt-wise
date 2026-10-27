using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Domain.Tests.Accounts;

public class AccountTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 2, 3, 4);

    [Fact]
    public void Create_makes_a_version_7_id()
    {
        Account account = Account.Create(Language.En, Now);

        Assert.Equal(7, account.Id.Version);
    }

    [Theory]
    [InlineData(Language.Lt)]
    [InlineData(Language.En)]
    public void Create_keeps_the_preferred_language(Language language)
    {
        Account account = Account.Create(language, Now);

        Assert.Equal(language, account.PreferredLanguage);
    }

    [Fact]
    public void Create_keeps_the_creation_time()
    {
        Account account = Account.Create(Language.En, Instant.FromUtc(2026, 1, 2, 3, 4));

        Assert.Equal(Instant.FromUtc(2026, 1, 2, 3, 4), account.CreatedAt);
    }

    [Fact]
    public void Create_starts_with_no_roles()
    {
        Account account = Account.Create(Language.En, Now);

        Assert.Empty(account.RoleIds);
    }

    [Fact]
    public void AddRole_keeps_two_different_roles()
    {
        Account account = Account.Create(Language.Lt, Now);
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();

        account.AddRole(first);
        account.AddRole(second);

        Assert.Equal(new HashSet<Guid> { first, second }, account.RoleIds.ToHashSet());
    }

    [Fact]
    public void AddRole_twice_with_the_same_role_adds_it_once()
    {
        Account account = Account.Create(Language.Lt, Now);
        Guid roleId = Guid.CreateVersion7();

        account.AddRole(roleId);
        account.AddRole(roleId);

        Assert.Equal([roleId], account.RoleIds);
    }

    [Fact]
    public void AddRole_rejects_an_empty_role_id()
    {
        Account account = Account.Create(Language.Lt, Now);

        Assert.Throws<ArgumentOutOfRangeException>("roleId", () => account.AddRole(Guid.Empty));
    }
}
