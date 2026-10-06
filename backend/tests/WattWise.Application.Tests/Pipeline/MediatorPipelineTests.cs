using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

public sealed class MediatorPipelineTests : IDisposable
{
    private readonly PipelineHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler_and_returns_its_value()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery("Ada"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, Ada", result.Value);
        Assert.Equal(1, _host.Recorder.Calls);
    }

    [Fact]
    public async Task Invalid_request_fails_with_a_validation_error_and_skips_the_handler()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery(string.Empty), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.NotNull(result.Error.FieldErrors);
        Assert.Contains("Name", result.Error.FieldErrors.Keys);
        Assert.NotEmpty(result.Error.FieldErrors["Name"]);
        Assert.Equal(0, _host.Recorder.Calls);
    }

    [Fact]
    public async Task Validation_collects_every_message_for_a_property()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery(new string('x', 20) + "1"), Token);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error.FieldErrors);
        Assert.Equal(2, result.Error.FieldErrors["Name"].Length);
        Assert.Contains("'Name' must not contain digits.", result.Error.FieldErrors["Name"]);
    }

    [Fact]
    public async Task Property_names_starting_with_an_acronym_are_left_unchanged()
    {
        Result<string> result = await _host.Mediator.Send(new AcronymQuery(null, null), Token);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error.FieldErrors);
        Assert.Equal(["IPAddress", "URL"], result.Error.FieldErrors.Keys.Order().ToArray());
        Assert.Equal(0, _host.Recorder.Calls);
    }

    [Fact]
    public async Task Nested_property_errors_use_the_validators_dotted_path()
    {
        Result<string> result = await _host.Mediator.Send(new CreateThingCommand(new Address(null)), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.FieldErrors);
        Assert.Contains("Address.StreetName", result.Error.FieldErrors.Keys);
        Assert.Equal(0, _host.Recorder.Calls);
    }

    [Fact]
    public async Task Request_without_a_validator_passes_through_and_keeps_its_error()
    {
        Result<string> result = await _host.Mediator.Send(new NotFoundQuery(), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Test.Missing", result.Error.Code);
        Assert.Null(result.Error.FieldErrors);
    }

    [Fact]
    public async Task Success_is_logged_as_handled()
    {
        await _host.Mediator.Send(new EchoQuery("Ada"), Token);

        FakeLogRecord record = Assert.Single(_host.LoggingBehaviorRecords());
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.StartsWith("Handled EchoQuery in ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_is_logged_with_its_error_code()
    {
        await _host.Mediator.Send(new NotFoundQuery(), Token);

        FakeLogRecord record = Assert.Single(_host.LoggingBehaviorRecords());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Contains("failed with Test.Missing", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exception_propagates_and_the_logging_behavior_logs_nothing()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _host.Mediator.Send(new ThrowingQuery(), Token));

        Assert.Equal("Boom.", exception.Message);
        Assert.Empty(_host.LoggingBehaviorRecords());
    }
}
