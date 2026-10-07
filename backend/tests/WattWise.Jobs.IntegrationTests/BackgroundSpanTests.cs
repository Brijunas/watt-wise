using System.Collections.Concurrent;
using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Trace;

using WattWise.Infrastructure.Observability;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class BackgroundSpanTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hangfire_polling_exports_no_parentless_database_spans()
    {
        ParentlessNpgsqlSpans spans = new();
        await using TestHostFactory<Program> factory = new(
            fixture.Database.ConfigurationFor(DatabaseRole.Hangfire),
            services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(spans)));
        using HttpClient client = factory.CreateClient();

        // The idle server polls its storage, so parentless Npgsql spans appear within seconds.
        await Poll.UntilAsync(() => spans.Seen.IsEmpty ? null : spans, "a parentless Npgsql span", Token, TimeSpan.FromSeconds(30));

        // Activity listeners are process-wide, but every Jobs host in this assembly has the filter.
        Assert.All(spans.Seen, span => Assert.False(span.Recorded));
    }

    private sealed class ParentlessNpgsqlSpans : BaseProcessor<Activity>
    {
        public ConcurrentQueue<Activity> Seen { get; } = new();

        public override void OnEnd(Activity data)
        {
            if (data.Source.Name == ParentlessDatabaseSpanFilter.NpgsqlSource && data.ParentSpanId == default)
            {
                Seen.Enqueue(data);
            }
        }
    }
}
