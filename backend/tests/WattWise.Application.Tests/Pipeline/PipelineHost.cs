using FluentValidation;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using WattWise.Application.Behaviors;

namespace WattWise.Application.Tests.Pipeline;

/// <summary>
/// A service provider wired like the hosts: the real generated mediator with the production
/// behaviors, plus a fake logger and a call recorder, one instance per test.
/// </summary>
public sealed class PipelineHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public PipelineHost()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddFakeLogging());
        services.AddSingleton<CallRecorder>();
        services.AddApplication();
        services.AddValidatorsFromAssemblyContaining<EchoQueryValidator>();
        // Keep the behavior list identical to Program.cs in Api (the generator needs a literal list at each site).
        services.AddMediator((MediatorOptions options) =>
        {
            options.Assemblies = [typeof(ApplicationAssembly), typeof(PipelineHost)];
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public IMediator Mediator => _scope.ServiceProvider.GetRequiredService<IMediator>();

    public CallRecorder Recorder => _provider.GetRequiredService<CallRecorder>();

    public FakeLogCollector Logs => _provider.GetRequiredService<FakeLogCollector>();

    /// <summary>The records the logging behavior wrote, whatever the request type.</summary>
    public IReadOnlyList<FakeLogRecord> LoggingBehaviorRecords()
    {
        return [.. Logs.GetSnapshot().Where(record => record.Category!.Contains("LoggingBehavior", StringComparison.Ordinal))];
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
