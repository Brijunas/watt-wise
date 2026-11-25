using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WattWise.Testing;

/// <summary>
/// Records the exception the host's start-up validators (<c>ValidateOnStart</c>) throw. A host built by
/// <c>WebApplicationFactory</c> from top-level code runs on its own thread, so the exception that
/// <c>CreateClient</c> surfaces can be an <see cref="ObjectDisposedException"/> when that thread has already
/// disposed the failed host. This capture sees the real validation failure whichever one surfaces.
/// Pass <see cref="Decorate"/> as the factory's <c>configureServices</c> and check the outcome with
/// <see cref="AssertValidationFailure"/>. The race is in <c>DeferredHost.StartAsync</c> (the
/// <c>DeferredHostBuilder</c> that <c>Microsoft.AspNetCore.Mvc.Testing</c> uses for top-level programs),
/// which reads <c>_host.Services</c> (<c>GetRequiredService&lt;IHostApplicationLifetime&gt;</c>) after that thread has
/// already disposed the failed host, and so throws <see cref="ObjectDisposedException"/>.
/// </summary>
public sealed class StartupFailureCapture
{
    private Exception? _captured;

    /// <summary>The first exception a start-up validator threw, or null when none did.</summary>
    public Exception? Captured => Volatile.Read(ref _captured);

    /// <summary>
    /// Asserts that starting the host threw (<paramref name="thrown"/>, whatever its type) and that a start-up
    /// validator threw an <see cref="OptionsValidationException"/>; returns that exception for the caller to
    /// assert on its message.
    /// </summary>
    public OptionsValidationException AssertValidationFailure(Exception? thrown)
    {
        Assert.NotNull(thrown);
        Exception? captured = Captured;
        if (captured is null)
        {
            Assert.Fail("No start-up validator threw: the host failed before its start-up validators ran.");
        }

        OptionsValidationException? validation = ExceptionChain.Of(captured).OfType<OptionsValidationException>().FirstOrDefault();
        Assert.NotNull(validation);
        return validation;
    }

    public void Decorate(IServiceCollection services)
    {
        // The sync interface is obsolete but the host still calls it when registered.
#pragma warning disable SYSLIB0066
        Wrap<IStartupValidator>(services, inner => new SyncValidator(inner, this));
#pragma warning restore SYSLIB0066
        Wrap<IAsyncStartupValidator>(services, inner => new AsyncValidator(inner, this));
    }

    private void Record(Exception exception) => Interlocked.CompareExchange(ref _captured, exception, null);

    private static void Wrap<TService>(IServiceCollection services, Func<TService, TService> decorate)
        where TService : class
    {
        foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
        {
            if (descriptor.IsKeyedService)
            {
                throw new NotSupportedException($"Keyed {typeof(TService).Name} registrations are not supported by {nameof(StartupFailureCapture)}.");
            }

            services.Remove(descriptor);
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                provider => decorate((TService)Resolve(provider, descriptor)),
                descriptor.Lifetime));
        }
    }

    private static object Resolve(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);

#pragma warning disable SYSLIB0066
    private sealed class SyncValidator(IStartupValidator inner, StartupFailureCapture capture) : IStartupValidator
    {
        public void Validate()
        {
            try
            {
                inner.Validate();
            }
            catch (Exception exception)
            {
                capture.Record(exception);
                throw;
            }
        }
    }
#pragma warning restore SYSLIB0066

    private sealed class AsyncValidator(IAsyncStartupValidator inner, StartupFailureCapture capture) : IAsyncStartupValidator
    {
        public async Task ValidateAsync(CancellationToken cancellationToken)
        {
            try
            {
                await inner.ValidateAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                capture.Record(exception);
                throw;
            }
        }
    }
}
