using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Tests.Persistence.Schema;

public class DatabaseMigratorTests
{
    private static DatabaseMigrator Build(params ISchemaStep[] steps) => new(steps, NullLogger<DatabaseMigrator>.Instance);

    // Listeners are process-wide, so spans are filtered by the trace of a root span this test starts.
    private static async Task<(Activity[] Spans, Exception? Error)> RunRecordedAsync(DatabaseMigrator migrator)
    {
        using ActivitySource testSource = new("WattWise.Tests.MigratorUnit");
        ConcurrentQueue<Activity> stopped = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name is "WattWise.Infrastructure" or "WattWise.Tests.MigratorUnit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity? root = testSource.StartActivity("migrate");
        Assert.NotNull(root);
        Exception? error = null;
        try
        {
            await migrator.MigrateAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        return ([.. stopped.Where(span => span.Parent == root)], error);
    }

    // characterization: DatabaseMigrator.cs:22-23 (span name format and the tag key "wattwise.schema_step")
    [Fact]
    public async Task Each_span_carries_the_step_name_tag()
    {
        List<string> log = [];
        DatabaseMigrator migrator = Build(new FakeSchemaStep("first", log), new FakeSchemaStep("second", log));

        (Activity[] spans, _) = await RunRecordedAsync(migrator);

        Assert.Equal(
            [("schema-step first", "first"), ("schema-step second", "second")],
            spans.Select(span => (span.DisplayName, (string?)span.GetTagItem("wattwise.schema_step"))).ToArray());
    }

    // characterization: DatabaseMigrator.cs:30 (the description text "Schema step bad failed.")
    [Fact]
    public async Task A_failing_step_marks_its_span_Error_records_the_exception_and_rethrows()
    {
        List<string> log = [];
        InvalidOperationException failure = new("boom");
        DatabaseMigrator migrator = Build(new FakeSchemaStep("bad", log, failure), new FakeSchemaStep("next", log));

        (Activity[] spans, Exception? error) = await RunRecordedAsync(migrator);

        Activity span = spans.Single();
        Assert.Equal(
            (failure, ActivityStatusCode.Error, "Schema step bad failed.", "System.InvalidOperationException", "bad"),
            (error, span.Status, span.StatusDescription, span.Events.Single(e => e.Name == "exception").Tags.Single(t => t.Key == "exception.type").Value as string, string.Join(",", log)));
    }

    [Fact]
    public async Task A_cancelled_token_stops_before_the_first_step()
    {
        List<string> log = [];
        DatabaseMigrator migrator = Build(new FakeSchemaStep("first", log));
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Exception? ex = await Record.ExceptionAsync(() => migrator.MigrateAsync(cts.Token));

        Assert.Equal((typeof(OperationCanceledException), 0), (ex?.GetType(), log.Count));
    }
}
