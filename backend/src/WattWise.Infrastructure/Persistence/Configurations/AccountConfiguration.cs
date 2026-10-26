using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using WattWise.Domain.Accounts;

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

        builder.HasMany(account => account.Roles)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "account_roles",
                right => right.HasOne<Role>().WithMany().HasForeignKey("role_id").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Account>().WithMany().HasForeignKey("account_id").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("account_id", "role_id"));
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
