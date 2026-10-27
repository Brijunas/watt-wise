namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>The two kinds of email link; a Theory takes one so its body never branches.</summary>
public enum EmailLinkKind
{
    Confirmation,
    Reset,
}
