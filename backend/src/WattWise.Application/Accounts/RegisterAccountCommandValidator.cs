using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator(IPasswordPolicy passwordPolicy)
    {
        RuleFor(command => command.Email).MustBeEmail();

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .CustomAsync(async (password, context, cancellationToken) =>
            {
                foreach (string message in await passwordPolicy.CheckAsync(password!, cancellationToken))
                {
                    context.AddFailure(message);
                }
            });

        RuleFor(command => command.Language)
            .Must(language => RegisterAccountCommandHandler.ParseLanguage(language) is not null)
            .WithMessage("'{PropertyName}' must be 'lt' or 'en'.");
    }
}
