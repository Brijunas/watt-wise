namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>A way to alter a real token so it is no longer the canonical form; its text names the case in the test output.</summary>
public sealed record NotCanonicalToken(string Description, Func<string, string> Apply)
{
    public override string ToString() => Description;
}
