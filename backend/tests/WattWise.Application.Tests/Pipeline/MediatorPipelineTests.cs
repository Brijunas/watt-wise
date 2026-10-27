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
    public async Task A_valid_request_reaches_the_handler_and_returns_its_value()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery("Ada"), Token);

        Assert.Equal((true, "Hello, Ada", 1), (result.IsSuccess, result.Value, _host.Recorder.Calls));
    }

    [Fact]
    public async Task An_invalid_request_fails_with_the_validation_code_and_the_exact_message_per_property()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery(string.Empty), Token);

        Assert.Equal(
            ("Validation.Failed", ErrorType.Validation, "Name: 'Name' must not be empty."),
            (result.Error.Code, result.Error.Type, string.Join(" | ", result.Error.FieldErrors!.SelectMany(pair => pair.Value.Select(message => $"{pair.Key}: {message}")))));
    }

    [Fact]
    public async Task An_invalid_request_never_reaches_the_handler()
    {
        await _host.Mediator.Send(new EchoQuery(string.Empty), Token);

        Assert.Equal(0, _host.Recorder.Calls);
    }

    [Fact]
    public async Task Every_message_for_one_property_is_kept_in_rule_order()
    {
        Result<string> result = await _host.Mediator.Send(new EchoQuery(new string('x', 20) + "1"), Token);

        Assert.Equal(
            [
                "The length of 'Name' must be 20 characters or fewer. You entered 21 characters.",
                "'Name' must not contain digits.",
            ],
            result.Error.FieldErrors!["Name"]);
    }

    [Fact]
    public async Task Property_names_starting_with_an_acronym_are_left_unchanged()
    {
        Result<string> result = await _host.Mediator.Send(new AcronymQuery(null, null), Token);

        Assert.Equal(["IPAddress", "URL"], result.Error.FieldErrors!.Keys.Order().ToArray());
    }

    [Fact]
    public async Task A_nested_property_keeps_the_validators_dotted_path()
    {
        Result<string> result = await _host.Mediator.Send(new CreateThingCommand(new Address(null)), Token);

        Assert.Equal(["Address.StreetName"], result.Error.FieldErrors!.Keys.ToArray());
    }

    [Fact]
    public async Task A_request_without_validators_keeps_its_handlers_error()
    {
        Result<string> result = await _host.Mediator.Send(new NotFoundQuery(), Token);

        Assert.Equal(new Error("Test.Missing", "The thing is missing.", ErrorType.NotFound), result.Error);
    }

    [Fact]
    public async Task A_success_logs_one_debug_line_naming_the_request()
    {
        await _host.Mediator.Send(new EchoQuery("Ada"), Token);

        Assert.Equal(
            [(LogLevel.Debug, "EchoQuery")],
            _host.LoggingBehaviorRecords().Select(record => (record.Level, Structured(record, "RequestName"))));
    }

    [Fact]
    public async Task A_failure_logs_one_information_line_with_the_error_code()
    {
        await _host.Mediator.Send(new NotFoundQuery(), Token);

        Assert.Equal(
            [(LogLevel.Information, "NotFoundQuery", "Test.Missing")],
            _host.LoggingBehaviorRecords().Select(record => (record.Level, Structured(record, "RequestName"), Structured(record, "ErrorCode"))));
    }

    [Fact]
    public async Task An_exception_from_the_handler_propagates_unchanged()
    {
        Assert.Equal(
            "Boom.",
            (await Assert.ThrowsAsync<InvalidOperationException>(async () => await _host.Mediator.Send(new ThrowingQuery(), Token))).Message);
    }

    [Fact]
    public async Task The_logging_behavior_logs_nothing_for_an_exception()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _host.Mediator.Send(new ThrowingQuery(), Token));

        Assert.Empty(_host.LoggingBehaviorRecords());
    }

    private static string? Structured(FakeLogRecord record, string key)
    {
        return record.StructuredState!.Single(pair => pair.Key == key).Value;
    }
}
