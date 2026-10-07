using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WattWise.Jobs.IntegrationTests;

/// <summary>
/// TestServer leaves the remote address empty, which Hangfire's default local-requests-only dashboard filter rejects.
/// This makes every request look like it comes from the loopback address, as a real local request does.
/// </summary>
public sealed class LoopbackClient : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                await nextMiddleware();
            });
            next(app);
        };
}
