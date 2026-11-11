using Mediator;

using WattWise.Application.Email;
using WattWise.Application.Results;
using WattWise.Domain.Localization;

namespace WattWise.Application.Accounts;

public sealed class RegisterAccountCommandHandler(
    IEmailAddressNormalizer normalizer,
    IAccountRegistration registration,
    IEmailRequests emailRequests,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterAccountCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        string email = normalizer.Normalize(command.Email!);
        string password = command.Password!;
        Language language = ParseLanguage(command.Language)!.Value;

        Guid? existing = await registration.FindAccountIdAsync(email, cancellationToken);
        if (existing is { } existingId)
        {
            registration.HashAndDiscard(password);
            emailRequests.Add(EmailRequestKind.AccountAlreadyExists, existingId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        Guid accountId = registration.StageAccount(language);
        emailRequests.Add(EmailRequestKind.EmailConfirmation, accountId);

        // False means another request took the address between the lookup and the save and nothing was
        // saved. There is nothing to do: the response stays the same, so nobody learns anything from it.
        _ = await registration.CreateIdentityAsync(accountId, email, password, cancellationToken);
        return Unit.Value;
    }

    internal static Language? ParseLanguage(string? language)
    {
        return language switch
        {
            "lt" => Language.Lt,
            "en" => Language.En,
            _ => null,
        };
    }
}
