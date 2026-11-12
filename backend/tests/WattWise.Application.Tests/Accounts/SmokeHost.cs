using Mediator;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The real generated mediator and production behaviors with the smoke account, role, session and transaction ports faked;
/// every port call goes to <see cref="Calls"/>.
/// </summary>
internal sealed class SmokeHost : IDisposable
{
    public static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private readonly PipelineHost _host;

    public SmokeHost(params string[] passwordPolicyMessages)
    {
        Calls = [];
        Accounts = new RecordingSmokeAccounts(Calls);
        Roles = new RecordingAccountRoles(Calls);
        _host = new PipelineHost(services =>
        {
            services.AddSingleton<IEmailAddressNormalizer, PrefixingEmailNormalizer>();
            services.AddSingleton<IPasswordPolicy>(new FixedPasswordPolicy(Calls, passwordPolicyMessages));
            services.AddSingleton<ISmokeAccounts>(Accounts);
            services.AddSingleton<IAccountRoles>(Roles);
            services.AddSingleton<IAccountSessions>(new RecordingAccountSessions(Calls));
            services.AddSingleton<ITransactionRunner>(new RecordingTransactionRunner(Calls));
        });
    }

    public List<string> Calls { get; }

    public RecordingSmokeAccounts Accounts { get; }

    public RecordingAccountRoles Roles { get; }

    public IMediator Mediator => _host.Mediator;

    public void Dispose() => _host.Dispose();
}
