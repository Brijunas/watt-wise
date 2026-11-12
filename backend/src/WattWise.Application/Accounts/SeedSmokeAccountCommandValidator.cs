using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class SeedSmokeAccountCommandValidator : AbstractValidator<SeedSmokeAccountCommand>
{
    public SeedSmokeAccountCommandValidator(IPasswordPolicy passwordPolicy)
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
    }
}
