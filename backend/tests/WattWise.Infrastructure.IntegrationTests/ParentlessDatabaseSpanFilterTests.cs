using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Trace;

using WattWise.Infrastructure.Observability;

namespace WattWise.Infrastructure.IntegrationTests;

public class ParentlessDatabaseSpanFilterTests
{
    private const string ParentSource = "WattWise.Tests.SpanFilter";

    [Fact]
    public void A_database_span_without_a_parent_is_not_recorded_and_one_inside_a_parent_is()
    {
        using ActivitySource npgsql = new(ParentlessDatabaseSpanFilter.NpgsqlSource);
        using ActivitySource parentSource = new(ParentSource);
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ParentlessDatabaseSpanFilter.NpgsqlSource, ParentSource)
            .AddProcessor(new ParentlessDatabaseSpanFilter())
            .Build();

        using (Activity? orphan = npgsql.StartActivity("SELECT"))
        {
            Assert.NotNull(orphan);
            Assert.False(orphan.Recorded);
        }

        using Activity? parent = parentSource.StartActivity("request");
        using Activity? child = npgsql.StartActivity("SELECT");
        Assert.NotNull(child);
        Assert.True(child.Recorded);
    }
}
