namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>One parsed <c>Set-Cookie</c> header: its name and value, and its attributes by lowercase name (flags have an empty value).</summary>
public sealed record SetCookieHeader(string Name, string Value, IReadOnlyDictionary<string, string> Attributes)
{
    public const string RefreshCookieName = "__Secure-wattwise-refresh";

    public const string AdminRefreshCookieName = "__Secure-wattwise-admin-refresh";

    public string NameAndValue => $"{Name}={Value}";

    public static SetCookieHeader Parse(string header)
    {
        string[] parts = header.Split(';', StringSplitOptions.TrimEntries);
        int equals = parts[0].IndexOf('=', StringComparison.Ordinal);
        Dictionary<string, string> attributes = [];
        foreach (string part in parts.Skip(1))
        {
            int separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                attributes[part.ToLowerInvariant()] = string.Empty;
            }
            else
            {
                attributes[part[..separator].ToLowerInvariant()] = part[(separator + 1)..];
            }
        }

        return new SetCookieHeader(parts[0][..equals], parts[0][(equals + 1)..], attributes);
    }

    /// <summary>
    /// The refresh cookie the response sets, described as <c>value|path|httponly|secure|samesite|expiry date</c>, so a test can
    /// compare a clearing cookie with <see cref="Cleared"/> in one assert; null when it sets none.
    /// </summary>
    public static string? DescribeRefreshCookieOf(HttpResponseMessage response, string name = RefreshCookieName)
    {
        SetCookieHeader? cookie = RefreshCookieOf(response, name);
        if (cookie is null)
        {
            return null;
        }

        string expires = cookie.Attributes.TryGetValue("expires", out string? value)
            ? DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "none";
        return $"{cookie.Value}|{cookie.Attributes.GetValueOrDefault("path")}|{cookie.Attributes.ContainsKey("httponly")}|{cookie.Attributes.ContainsKey("secure")}|{cookie.Attributes.GetValueOrDefault("samesite")}|{expires}";
    }

    /// <summary>What <see cref="DescribeRefreshCookieOf"/> gives for a cookie that clears the refresh cookie: empty, on its path, protected, expired in 1970.</summary>
    public const string Cleared = "|/api/v1/auth|True|True|strict|1970-01-01";

    /// <summary>The refresh cookie the response sets, or null when it sets none.</summary>
    public static SetCookieHeader? RefreshCookieOf(HttpResponseMessage response, string name = RefreshCookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? headers))
        {
            return null;
        }

        return headers.Select(Parse).SingleOrDefault(cookie => cookie.Name == name);
    }
}
