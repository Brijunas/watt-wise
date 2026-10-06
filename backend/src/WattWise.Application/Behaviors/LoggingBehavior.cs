using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.Logging;

using WattWise.Application.Results;

namespace WattWise.Application.Behaviors;

/// <summary>
/// Logs the outcome and duration of every use case. Exceptions are not logged here: they
/// propagate and the exception middleware logs them once.
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IFallibleResult<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        TResponse response = await next(message, cancellationToken);
        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (response.IsFailure)
        {
            LogFailed(typeof(TMessage).Name, response.Error.Code, elapsedMs);
        }
        else
        {
            LogHandled(typeof(TMessage).Name, elapsedMs);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms")]
    private partial void LogHandled(string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestName} failed with {ErrorCode} in {ElapsedMs} ms")]
    private partial void LogFailed(string requestName, string errorCode, double elapsedMs);
}
