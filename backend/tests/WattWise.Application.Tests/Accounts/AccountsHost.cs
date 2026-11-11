using Mediator;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The real generated mediator and production behaviors with the account ports faked; every port call goes to <see cref="Calls"/>.</summary>
internal sealed class AccountsHost : IDisposable
{
    private readonly PipelineHost _host;

    public AccountsHost(params string[] passwordMessages)
    {
        Calls = [];
        Registration = new RecordingAccountRegistration(Calls);
        Confirmation = new RecordingEmailConfirmation(Calls);
        _host = new PipelineHost(services =>
        {
            services.AddSingleton<IEmailAddressNormalizer, PrefixingEmailNormalizer>();
            services.AddSingleton<IAccountRegistration>(Registration);
            services.AddSingleton<IEmailConfirmation>(Confirmation);
            services.AddSingleton<IEmailRequests>(new RecordingEmailRequests(Calls));
            services.AddSingleton<IUnitOfWork>(new RecordingUnitOfWork(Calls));
            services.AddSingleton<IPasswordPolicy>(new FixedPasswordPolicy(Calls, passwordMessages));
        });
    }

    public List<string> Calls { get; }

    public RecordingAccountRegistration Registration { get; }

    public RecordingEmailConfirmation Confirmation { get; }

    public IMediator Mediator => _host.Mediator;

    public void Dispose() => _host.Dispose();
}
