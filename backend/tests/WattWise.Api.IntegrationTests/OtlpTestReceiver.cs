using System.Collections.Concurrent;
using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// A stand-in for an OTLP/gRPC collector: listens for HTTP/2 without TLS on a free loopback port,
/// answers every call with an empty successful gRPC response and records the request paths.
/// </summary>
public sealed class OtlpTestReceiver : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly ConcurrentQueue<string> paths;

    private OtlpTestReceiver(WebApplication app, ConcurrentQueue<string> paths, Uri endpoint)
    {
        this.app = app;
        this.paths = paths;
        Endpoint = endpoint;
    }

    public Uri Endpoint { get; }

    public IReadOnlyList<string> Paths()
    {
        return [.. paths];
    }

    public static async Task<OtlpTestReceiver> StartAsync(CancellationToken cancellationToken)
    {
        ConcurrentQueue<string> paths = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(
            IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        WebApplication app = builder.Build();
        app.Run(async context =>
        {
            paths.Enqueue(context.Request.Path.Value ?? string.Empty);
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);
            context.Response.ContentType = "application/grpc";
            // One empty gRPC message: not compressed (0), length 0.
            await context.Response.Body.WriteAsync(new byte[5], context.RequestAborted);
            context.Response.AppendTrailer("grpc-status", "0");
        });
        await app.StartAsync(cancellationToken);

        return new OtlpTestReceiver(app, paths, new Uri(app.Urls.First()));
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
    }
}
