using FluentValidation;

namespace WattWise.Application.Accounts;

/// <summary>Checks that both fields are present and not oversized; the password rules don't apply to sign-in.</summary>
public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        // The maximums keep an oversized value from reaching the lookup or the password hasher; they are the register rules' (254, 256).
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(256);
    }
}
