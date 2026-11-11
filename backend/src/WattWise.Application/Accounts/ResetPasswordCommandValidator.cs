using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IPasswordPolicy passwordPolicy)
    {
        RuleFor(command => command.Id)
            .Must(id => Guid.TryParse(id, out Guid parsed) && parsed != Guid.Empty)
            .WithMessage("'{PropertyName}' must be an id.");

        // An empty token is a bad link, not a bad request: the handler rejects it like any other.
        RuleFor(command => command.Token).NotNull();

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
