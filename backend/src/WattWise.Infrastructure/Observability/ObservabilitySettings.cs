namespace WattWise.Infrastructure.Observability;

/// <summary>Observability settings, read from the "Observability" configuration section.</summary>
public sealed class ObservabilitySettings
{
    public const string SectionName = "Observability";

    /// <summary>
    /// The OTLP gRPC endpoint (an origin such as http://host:4317) that logs, traces and metrics
    /// are exported to. Blank turns the exporters off; logs still go to the console.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>The endpoint as a URI, or null when it is blank or invalid (see <see cref="ObservabilitySettingsValidator"/>).</summary>
    public Uri? ValidOtlpEndpoint()
    {
        if (string.IsNullOrWhiteSpace(OtlpEndpoint)
            || !Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        bool valid = (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.AbsolutePath == "/"
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.UserInfo.Length == 0;
        return valid ? uri : null;
    }
}
