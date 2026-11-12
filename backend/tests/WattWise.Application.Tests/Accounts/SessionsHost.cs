using Mediator;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Accounts;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The real generated mediator and production behaviors with the sign-in ports faked, time fixed at
/// 2026-11-11T12:00Z. Every port call that changes anything goes to <see cref="Calls"/>.
/// </summary>
internal sealed class SessionsHost : IDisposable
{
    public static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    private readonly PipelineHost _host;

    /// <param name="passwordCheck">What the password check answers.</param>
    /// <param name="passwordMessages">What the password policy rejects every password with; none by default.</param>
    public SessionsHost(PasswordCheck? passwordCheck = null, params string[] passwordMessages)
    {
        Clock = new FakeClock(Now);
        Calls = [];
        PasswordChange = new ScriptedPasswordChange(Calls);
        PasswordReset = new ScriptedPasswordReset(Calls);
        Sessions = new InMemorySessions(Calls);
        UnitOfWork = new ConflictingUnitOfWork(Calls, Sessions);
        Queries = new FakeAccountsQueries();
        AccessTokens = new RecordingAccessTokenIssuer(Calls);
        ExpiredSessions = new RecordingExpiredSessions();
        AccountSessions = new RecordingAccountSessions(Calls);
        _host = new PipelineHost(services =>
        {
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IEmailAddressNormalizer, PrefixingEmailNormalizer>();
            services.AddSingleton<IPasswordSignIn>(new RecordingPasswordSignIn(Calls, passwordCheck ?? PasswordCheck.InvalidCredentials()));
            services.AddSingleton<IAccountsQueries>(Queries);
            services.AddSingleton<IRefreshTokenFactory>(new CountingRefreshTokenFactory());
            services.AddSingleton<IDeviceLabels, FixedDeviceLabels>();
            services.AddSingleton<ISessionRepository>(Sessions);
            services.AddSingleton<IAccessTokenIssuer>(AccessTokens);
            services.AddSingleton<IExpiredSessions>(ExpiredSessions);
            services.AddSingleton<IUnitOfWork>(UnitOfWork);
            services.AddSingleton<IPasswordChange>(PasswordChange);
            services.AddSingleton<IPasswordReset>(PasswordReset);
            services.AddSingleton<IAccountSessions>(AccountSessions);
            services.AddSingleton<ITransactionRunner>(new RecordingTransactionRunner(Calls));
            services.AddSingleton<IPasswordPolicy>(new FixedPasswordPolicy([], passwordMessages));
        });
    }

    public ScriptedPasswordChange PasswordChange { get; }

    public ScriptedPasswordReset PasswordReset { get; }

    public FakeClock Clock { get; }

    public List<string> Calls { get; }

    public InMemorySessions Sessions { get; }

    public ConflictingUnitOfWork UnitOfWork { get; }

    /// <summary>The next <paramref name="count"/> saves lose a race with another request.</summary>
    public void ConflictNextSaves(int count) => UnitOfWork.ConflictNext(count);

    public FakeAccountsQueries Queries { get; }

    public RecordingAccessTokenIssuer AccessTokens { get; }

    public RecordingExpiredSessions ExpiredSessions { get; }

    public RecordingAccountSessions AccountSessions { get; }

    public IMediator Mediator => _host.Mediator;

    public void Dispose() => _host.Dispose();
}
