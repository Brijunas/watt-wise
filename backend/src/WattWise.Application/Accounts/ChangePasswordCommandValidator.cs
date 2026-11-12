using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IPasswordPolicy passwordPolicy)
    {
        RuleFor(command => command.CurrentPassword).NotEmpty();

        RuleFor(command => command.NewPassword)
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
