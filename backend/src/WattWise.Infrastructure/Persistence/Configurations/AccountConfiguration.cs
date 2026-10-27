using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.PreferredLanguage)
            .HasConversion(new ValueConverter<Language, string>(
                language => ToText(language),
                text => ToLanguage(text)))
            .IsRequired();
        builder.Property(account => account.CreatedAt).IsRequired();

        builder.HasMany<AccountRole>("_roles")
            .WithOne()
            .HasForeignKey(link => link.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_roles").AutoInclude();
        builder.Ignore(account => account.RoleIds);
    }

    private static string ToText(Language language)
    {
        return language switch
        {
            Language.Lt => "lt",
            Language.En => "en",
        };
    }

    private static Language ToLanguage(string text)
    {
        return text switch
        {
            "lt" => Language.Lt,
            "en" => Language.En,
            _ => throw new InvalidOperationException($"Unknown language '{text}'."),
        };
    }
}
