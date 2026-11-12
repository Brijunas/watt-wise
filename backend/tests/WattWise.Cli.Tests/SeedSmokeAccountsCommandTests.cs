using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;

namespace WattWise.Cli.Tests;

public class SeedSmokeAccountsCommandTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string Refusal = "seed smoke-accounts runs only in the Development environment.";
    private const string AdminRoleId = "0192e5a0-0000-7000-8000-000000000001";

    private static Dictionary<string, string?> Settings(string userEmail, string userPassword, string adminEmail, string adminPassword) =>
        new()
        {
            ["SmokeAccounts__UserEmail"] = userEmail,
            ["SmokeAccounts__UserPassword"] = userPassword,
            ["SmokeAccounts__AdminEmail"] = adminEmail,
            ["SmokeAccounts__AdminPassword"] = adminPassword,
        };

    private Task<CliRun> RunAsync(string environmentName, Dictionary<string, string?> settings) =>
        CliRun.StartAsync(CliRun.EnvironmentFor(fixture.Database, environmentName, settings), "seed", "smoke-accounts");

    private static string Unix(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task In_Development_it_creates_both_accounts_then_reports_them_unchanged_without_printing_the_emails_or_passwords()
    {
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string userEmail = StoredAccounts.UniqueEmail();
        string adminEmail = StoredAccounts.UniqueEmail();
        string userPassword = StoredAccounts.ValidPassword();
        string adminPassword = StoredAccounts.ValidPassword();
        Dictionary<string, string?> settings = Settings(userEmail, userPassword, adminEmail, adminPassword);

        CliRun first = await RunAsync("Development", settings);
        CliRun second = await RunAsync("Development", settings);

        string[] secrets = [userEmail, adminEmail, userPassword, adminPassword];
        string everything = first.StdOut + first.StdErr + second.StdOut + second.StdErr;
        Assert.Equal(
            (
                (0, "user: created\nadmin: created\n", ""),
                (0, "user: unchanged\nadmin: unchanged\n", ""),
                (PasswordCheckOutcome.Succeeded, PasswordCheckOutcome.Succeeded),
                ("", AdminRoleId),
                false),
            (
                (first.ExitCode, Unix(first.StdOut), first.StdErr),
                (second.ExitCode, Unix(second.StdOut), second.StdErr),
                (await StoredAccounts.SignInAsync(cli, userEmail, userPassword), await StoredAccounts.SignInAsync(cli, adminEmail, adminPassword)),
                (await StoredAccounts.RoleIdsAsync(cli, userEmail), await StoredAccounts.RoleIdsAsync(cli, adminEmail)),
                secrets.Any(secret => everything.Contains(secret, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public async Task Outside_Development_it_is_refused_with_exit_1_and_touches_no_account()
    {
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string userEmail = StoredAccounts.UniqueEmail();
        string adminEmail = StoredAccounts.UniqueEmail();

        CliRun run = await RunAsync(
            "Production", Settings(userEmail, StoredAccounts.ValidPassword(), adminEmail, StoredAccounts.ValidPassword()));

        Assert.Equal(
            (1, "", Refusal, 0L, 0L),
            (run.ExitCode, run.StdOut, run.StdErr.Trim(), await StoredAccounts.CountAsync(cli, userEmail), await StoredAccounts.CountAsync(cli, adminEmail)));
    }

    [Fact]
    public async Task Outside_Development_the_refusal_comes_before_any_settings_check()
    {
        CliRun run = await CliRun.StartAsync(new Dictionary<string, string?> { ["DOTNET_ENVIRONMENT"] = "Production" }, "seed", "smoke-accounts");

        Assert.Equal((1, Refusal), (run.ExitCode, run.StdErr.Trim()));
    }

    [Fact]
    public async Task Missing_settings_are_named_by_key_and_never_by_value_and_nothing_is_seeded()
    {
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string userEmail = StoredAccounts.UniqueEmail();

        CliRun run = await RunAsync("Development", Settings(userEmail, "", StoredAccounts.UniqueEmail(), StoredAccounts.ValidPassword()));

        Assert.Equal(
            (1, "", "SmokeAccounts configuration is missing or invalid: SmokeAccounts:UserPassword.", 0L),
            (run.ExitCode, run.StdOut, run.StdErr.Trim(), await StoredAccounts.CountAsync(cli, userEmail)));
    }

    [Fact]
    public async Task A_password_the_policy_rejects_fails_with_the_label_exit_1_and_seeds_nothing_after_it()
    {
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string adminEmail = StoredAccounts.UniqueEmail();

        CliRun run = await RunAsync("Development", Settings(StoredAccounts.UniqueEmail(), "weak", adminEmail, StoredAccounts.ValidPassword()));

        Assert.Equal(
            (1, true, 0L),
            (run.ExitCode, run.StdErr.StartsWith("user: Validation.Failed:", StringComparison.Ordinal), await StoredAccounts.CountAsync(cli, adminEmail)));
    }

    [Fact]
    public async Task An_invalid_admin_password_leaves_the_user_created_and_fails_the_admin()
    {
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string userEmail = StoredAccounts.UniqueEmail();
        string adminEmail = StoredAccounts.UniqueEmail();

        CliRun run = await RunAsync("Development", Settings(userEmail, StoredAccounts.ValidPassword(), adminEmail, "weak"));

        Assert.Equal(
            (1, "user: created", true, 1L, 0L),
            (
                run.ExitCode,
                Unix(run.StdOut).Split('\n')[0],
                run.StdErr.StartsWith("admin: Validation.Failed:", StringComparison.Ordinal),
                await StoredAccounts.CountAsync(cli, userEmail),
                await StoredAccounts.CountAsync(cli, adminEmail)));
    }

    [Fact]
    public async Task A_database_failure_exits_1_and_prints_no_email_or_password()
    {
        string userEmail = StoredAccounts.UniqueEmail();
        string adminEmail = StoredAccounts.UniqueEmail();
        string userPassword = StoredAccounts.ValidPassword();
        string adminPassword = StoredAccounts.ValidPassword();
        string databasePassword = $"wrong-{Guid.NewGuid():N}";
        Dictionary<string, string?> environment = CliRun.EnvironmentFor(
            fixture.Database, "Development", Settings(userEmail, userPassword, adminEmail, adminPassword));
        environment["Database__Password"] = databasePassword;

        CliRun run = await CliRun.StartAsync(environment, "seed", "smoke-accounts");

        string[] secrets = [userEmail, adminEmail, userPassword, adminPassword, databasePassword];
        string everything = run.StdOut + run.StdErr;
        Assert.Equal(
            (1, true, false),
            (run.ExitCode, run.StdOut.Contains("Seed command failed: ", StringComparison.Ordinal), secrets.Any(secret => everything.Contains(secret, StringComparison.OrdinalIgnoreCase))));
    }
}
