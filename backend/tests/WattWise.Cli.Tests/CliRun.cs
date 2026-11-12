using System.Diagnostics;
using System.Reflection;

namespace WattWise.Cli.Tests;

/// <summary>What one run of the <c>WattWise.Cli</c> process printed and returned.</summary>
internal sealed record CliRun(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>
    /// Runs <c>WattWise.Cli</c> (from the CLI project's output folder) with <paramref name="args"/>. The environment is the test process's
    /// minus any host environment name, plus <paramref name="environment"/>; settings reach the CLI as environment variables.
    /// </summary>
    public static async Task<CliRun> StartAsync(IReadOnlyDictionary<string, string?> environment, params string[] args)
    {
        ProcessStartInfo start = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(CliAssemblyPath());
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        _ = start.Environment.Remove("ASPNETCORE_ENVIRONMENT");
        _ = start.Environment.Remove("DOTNET_ENVIRONMENT");
        foreach ((string key, string? value) in environment)
        {
            start.Environment[key] = value;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The CLI did not start.");
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<string> stdOut = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stdErr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new CliRun(process.ExitCode, await stdOut, await stdErr);
    }

    /// <summary>
    /// <c>WattWise.Cli.dll</c> in the CLI project's own output folder, which the test project's csproj records as assembly
    /// metadata. A copy next to the tests would lack the packages that the tests get from the ASP.NET Core framework.
    /// </summary>
    private static string CliAssemblyPath() =>
        Path.Combine(
            typeof(CliRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "CliOutputDirectory").Value!,
            "WattWise.Cli.dll");

    /// <summary>The environment variables for a run: the <c>Database:*</c> settings of the clone, the environment name, extras.</summary>
    public static Dictionary<string, string?> EnvironmentFor(
        TestDatabase database, string environmentName, IReadOnlyDictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> environment = new() { ["DOTNET_ENVIRONMENT"] = environmentName };
        foreach ((string key, string? value) in database.ConfigurationFor(DatabaseRole.Cli))
        {
            environment[key.Replace(":", "__", StringComparison.Ordinal)] = value;
        }

        foreach ((string key, string? value) in extra ?? new Dictionary<string, string?>())
        {
            environment[key] = value;
        }

        return environment;
    }
}
