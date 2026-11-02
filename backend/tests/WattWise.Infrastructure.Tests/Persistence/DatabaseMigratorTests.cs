using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Tests.Persistence;

public class DatabaseMigratorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DatabaseMigrator MigratorOf(params ISchemaStep[] steps) => new(steps, NullLogger<DatabaseMigrator>.Instance);

    private static Task Boom() => throw new InvalidOperationException("boom");

    // characterization: DatabaseMigrator.cs:30 (the status description).
    [Fact]
    public async Task A_failing_step_marks_its_span_as_an_error()
    {
        using MigratorSpans spans = new();
        DatabaseMigrator migrator = MigratorOf(new FakeSchemaStep("boom", [], Boom));

        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(Token));

        Activity span = spans.Single();
        Assert.Equal((ActivityStatusCode.Error, "Schema step boom failed."), (span.Status, span.StatusDescription));
    }

    // characterization: DatabaseMigrator.cs:31 (the exception event).
    [Fact]
    public async Task A_failing_step_records_the_exception_on_its_span()
    {
        using MigratorSpans spans = new();
        DatabaseMigrator migrator = MigratorOf(new FakeSchemaStep("boom", [], Boom));

        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(Token));

        Assert.Equal(["exception"], spans.Single().Events.Select(e => e.Name));
    }

    // characterization: DatabaseMigrator.cs:32 (the rethrow).
    [Fact]
    public async Task A_failing_step_stops_the_steps_after_it()
    {
        List<string> executed = [];
        DatabaseMigrator migrator = MigratorOf(new FakeSchemaStep("first", executed, Boom), new FakeSchemaStep("second", executed));

        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(Token));

        Assert.Equal(["first"], executed);
    }

    // characterization: DatabaseMigrator.cs:20 (the cancellation check before each step).
    [Fact]
    public async Task A_cancelled_token_runs_no_step()
    {
        List<string> executed = [];
        DatabaseMigrator migrator = MigratorOf(new FakeSchemaStep("first", executed));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => migrator.MigrateAsync(cancelled.Token));

        Assert.Empty(executed);
    }

    // characterization: DatabaseMigrator.cs:23 (the tag name).
    [Fact]
    public async Task Each_step_span_is_tagged_with_the_step_name()
    {
        using MigratorSpans spans = new();
        DatabaseMigrator migrator = MigratorOf(new FakeSchemaStep("first", []));

        await migrator.MigrateAsync(Token);

        Assert.Equal("first", spans.Single().GetTagItem("wattwise.schema_step"));
    }
}
