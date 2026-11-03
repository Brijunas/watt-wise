using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WattWise.Infrastructure.Tests.Email;

/// <summary>
/// A minimal SMTP server on a loopback port that accepts one connection and answers the commands MailKit sends.
/// Each factory scripts one behavior; <see cref="Commands"/> records what the client sent.
/// </summary>
internal sealed class ScriptedSmtpServer : IDisposable
{
    private const string EhloWithAuth = "250-test\r\n250 AUTH PLAIN";

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<string> _commands = new();
    private readonly TaskCompletionSource _firstCommand = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Behavior _behavior;
    private readonly Task _serving;

    private ScriptedSmtpServer(Behavior behavior)
    {
        _behavior = behavior;
        _listener.Start();
        _serving = ServeAsync(_stop.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Every command line received, in order (message bodies excluded).</summary>
    public IReadOnlyList<string> Commands => [.. _commands];

    /// <summary>Completes when the first command (the client's EHLO) has arrived.</summary>
    public Task FirstCommandReceived => _firstCommand.Task;

    /// <summary>Accepts the whole session and the message.</summary>
    public static ScriptedSmtpServer Accepting() => new(new Behavior());

    /// <summary>Offers no AUTH and no STARTTLS in its EHLO reply.</summary>
    public static ScriptedSmtpServer WithoutAuth() => new(new Behavior { EhloReply = "250 test" });

    /// <summary>Answers MAIL FROM with <paramref name="mailFromReply"/>.</summary>
    public static ScriptedSmtpServer Rejecting(string mailFromReply) => new(new Behavior { MailFromReply = mailFromReply });

    /// <summary>Answers AUTH with 535.</summary>
    public static ScriptedSmtpServer RejectingCredentials() => new(new Behavior { AuthReply = "535 5.7.8 Authentication credentials invalid" });

    /// <summary>Closes the connection right after answering EHLO.</summary>
    public static ScriptedSmtpServer DroppingAfterEhlo() => new(new Behavior { DropAfterEhlo = true });

    /// <summary>Greets, then never answers a command.</summary>
    public static ScriptedSmtpServer Stalling() => new(new Behavior { Stall = true });

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            _serving.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException or SocketException)
        {
        }

        _stop.Dispose();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream, Encoding.ASCII);
        await using StreamWriter writer = new(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 test ESMTP");
        bool inBody = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (inBody)
            {
                inBody = line != ".";
                if (!inBody)
                {
                    await writer.WriteLineAsync("250 2.0.0 queued");
                }

                continue;
            }

            _commands.Enqueue(line);
            _firstCommand.TrySetResult();
            if (_behavior.Stall)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            inBody = line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase);
            await writer.WriteLineAsync(inBody ? "354 go ahead" : Reply(line));
            if (_behavior.DropAfterEhlo && line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
    }

    private string Reply(string command) => command switch
    {
        _ when command.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) => _behavior.EhloReply,
        _ when command.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) => _behavior.AuthReply,
        _ when command.StartsWith("MAIL", StringComparison.OrdinalIgnoreCase) => _behavior.MailFromReply,
        _ when command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase) => "221 bye",
        _ => "250 OK",
    };

    private sealed record Behavior
    {
        public string EhloReply { get; init; } = EhloWithAuth;

        public string AuthReply { get; init; } = "235 2.7.0 Authentication successful";

        public string MailFromReply { get; init; } = "250 OK";

        public bool DropAfterEhlo { get; init; }

        public bool Stall { get; init; }
    }
}
