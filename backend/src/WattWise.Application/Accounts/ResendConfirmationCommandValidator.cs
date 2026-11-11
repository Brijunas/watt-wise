using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator()
    {
        RuleFor(command => command.Email).MustBeEmail();
    }
}
