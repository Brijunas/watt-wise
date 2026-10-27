using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Persistence;

/// <summary>
/// The app schema. Derives from <see cref="IdentityUserContext{TUser, TKey}"/>, so ASP.NET Core Identity's own
/// role and claim tables are not in the model: authorization sits on the account (roles, permissions).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AccountIdentity, Guid>(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<EmailLinkToken> EmailLinkTokens => Set<EmailLinkToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Claims are not used: permissions come from the account's roles, not from ASP.NET Core Identity.
        modelBuilder.Ignore<IdentityUserClaim<Guid>>();
        modelBuilder.HasDefaultSchema(DatabaseSchemas.App);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
