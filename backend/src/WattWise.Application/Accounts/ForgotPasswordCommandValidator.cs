using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email).MustBeEmail();
    }
}
