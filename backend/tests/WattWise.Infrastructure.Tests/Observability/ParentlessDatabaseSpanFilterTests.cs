using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Trace;

using WattWise.Infrastructure.Observability;

namespace WattWise.Infrastructure.Tests.Observability;

public class ParentlessDatabaseSpanFilterTests
{
    private const string OtherSource = "WattWise.Tests.OtherSource";

    private static TracerProvider Build() => Sdk.CreateTracerProviderBuilder()
        .AddSource("Npgsql", OtherSource)
        .AddProcessor(new ParentlessDatabaseSpanFilter())
        .Build();

    [Fact]
    public void An_Npgsql_span_without_a_parent_is_not_recorded()
    {
        using ActivitySource npgsql = new("Npgsql");
        using TracerProvider provider = Build();

        using Activity? span = npgsql.StartActivity("SELECT");

        Assert.NotNull(span);

        Assert.False(span.Recorded);
    }

    [Fact]
    public void An_Npgsql_span_inside_a_parent_is_recorded()
    {
        using ActivitySource npgsql = new("Npgsql");
        using ActivitySource other = new(OtherSource);
        using TracerProvider provider = Build();
        using Activity? parent = other.StartActivity("request");

        using Activity? span = npgsql.StartActivity("SELECT");

        Assert.NotNull(span);

        Assert.True(span.Recorded);
    }

    [Fact]
    public void A_parentless_span_from_another_source_is_recorded()
    {
        using ActivitySource other = new(OtherSource);
        using TracerProvider provider = Build();

        using Activity? span = other.StartActivity("job");

        Assert.NotNull(span);

        Assert.True(span.Recorded);
    }
}
