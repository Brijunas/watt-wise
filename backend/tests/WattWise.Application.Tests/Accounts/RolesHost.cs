using Mediator;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The real generated mediator and production behaviors with the role ports faked; every port call goes to
/// <see cref="Calls"/>. The account <c>ada@example.com</c> exists, confirmed, under <see cref="AccountId"/>.
/// </summary>
internal sealed class RolesHost : IDisposable
{
    public static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private readonly PipelineHost _host;

    public RolesHost(bool emailConfirmed = true)
    {
        Calls = [];
        Roles = new RecordingAccountRoles(Calls)
        {
            Known = ("normalized:ada@example.com", new RoleHolder(AccountId, emailConfirmed)),
        };
        _host = new PipelineHost(services =>
        {
            services.AddSingleton<IEmailAddressNormalizer, PrefixingEmailNormalizer>();
            services.AddSingleton<IAccountRoles>(Roles);
            services.AddSingleton<IAccountSessions>(new RecordingAccountSessions(Calls));
            services.AddSingleton<ITransactionRunner>(new RecordingTransactionRunner(Calls));
        });
    }

    public List<string> Calls { get; }

    public RecordingAccountRoles Roles { get; }

    public IMediator Mediator => _host.Mediator;

    public void Dispose() => _host.Dispose();
}
