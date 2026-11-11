using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>Labels a user agent <c>label:&lt;agent&gt;</c> (<c>label:</c> for none), so a test sees what the handler passed on.</summary>
internal sealed class FixedDeviceLabels : IDeviceLabels
{
    public string FromUserAgent(string? userAgent) => $"label:{userAgent}";
}
