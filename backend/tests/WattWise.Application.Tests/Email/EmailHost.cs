using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Application.Tests.Pipeline;
using WattWise.Domain.Localization;

namespace WattWise.Application.Tests.Email;

/// <summary>
/// The real generated mediator and production behaviors with the outbox and the sender faked, time fixed at
/// 2026-11-11T12:00Z, and the retry schedules from the spec's table (<see cref="SpecRetrySettings"/>).
/// </summary>
internal sealed class EmailHost : IDisposable
{
    public static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    private readonly PipelineHost _host;

    public EmailHost()
    {
        Clock = new FakeClock(Now);
        Calls = [];
        Outbox = new RecordingOutbox(Calls);
        Sender = new ScriptedEmailSender(Calls);
        _host = new PipelineHost(services =>
        {
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IEmailOutbox>(Outbox);
            services.AddSingleton<IEmailSender>(Sender);
            services.AddSingleton<IEmailRetrySettings>(new SpecRetrySettings());
        });
    }

    public FakeClock Clock { get; }

    public List<string> Calls { get; }

    public RecordingOutbox Outbox { get; }

    public ScriptedEmailSender Sender { get; }

    public IMediator Mediator => _host.Mediator;

    public FakeLogCollector Logs => _host.Logs;

    /// <summary>A claimed request as the outbox hands it over.</summary>
    public ReadyEmail Ready(EmailRequestKind kind, int attempt, string to = "ada@example.com", Language language = Language.Lt)
    {
        EmailMessage message = kind switch
        {
            EmailRequestKind.EmailConfirmation => new EmailConfirmationEmail(to, language, new Uri("https://app.wattwise.test/confirm-email?id=1&token=t")),
            EmailRequestKind.PasswordReset => new PasswordResetEmail(to, language, new Uri("https://app.wattwise.test/reset-password?id=1&token=t")),
            _ => new AccountAlreadyExistsEmail(to, language),
        };
        return new ReadyEmail(Guid.CreateVersion7(), kind, attempt, message);
    }

    public void Dispose() => _host.Dispose();
}
