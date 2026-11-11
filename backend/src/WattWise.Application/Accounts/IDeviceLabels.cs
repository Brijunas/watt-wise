namespace WattWise.Application.Accounts;

/// <summary>Turns a User-Agent header into the label a session shows, such as "Firefox on Linux".</summary>
public interface IDeviceLabels
{
    string FromUserAgent(string? userAgent);
}
