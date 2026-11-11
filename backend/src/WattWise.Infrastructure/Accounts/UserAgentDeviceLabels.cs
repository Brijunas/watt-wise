using MyCSharp.HttpUserAgentParser;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Labels a session by browser and operating system, e.g. "Firefox on Linux".</summary>
internal sealed class UserAgentDeviceLabels : IDeviceLabels
{
    public const string Unknown = "Unknown device";

    private const int MaxLength = 100;

    public string FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return Unknown;
        }

        HttpUserAgentInformation information = HttpUserAgentParser.Parse(userAgent);
        string? name = information.Name;
        string? platform = information.Platform?.Name;
        if (information.Type == HttpUserAgentType.Unknown || string.IsNullOrWhiteSpace(name))
        {
            return Unknown;
        }

        string label = string.IsNullOrWhiteSpace(platform) ? name : $"{name} on {platform}";
        return label.Length <= MaxLength ? label : label[..MaxLength];
    }
}
