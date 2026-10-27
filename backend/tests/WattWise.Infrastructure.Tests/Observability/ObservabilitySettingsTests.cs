using WattWise.Infrastructure.Observability;

namespace WattWise.Infrastructure.Tests.Observability;

public class ObservabilitySettingsTests
{
    [Theory]
    [InlineData("http://127.0.0.1:4317", "http://127.0.0.1:4317/")]
    [InlineData("https://collector", "https://collector/")]
    [InlineData(null, null)]
    public void ValidOtlpEndpoint_gives_the_uri_or_null(string? endpoint, string? expected)
    {
        Uri? uri = new ObservabilitySettings { OtlpEndpoint = endpoint }.ValidOtlpEndpoint();

        Assert.Equal(expected, uri?.AbsoluteUri);
    }
}
