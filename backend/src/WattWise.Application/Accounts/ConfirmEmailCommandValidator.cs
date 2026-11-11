using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => Guid.TryParse(id, out Guid parsed) && parsed != Guid.Empty)
            .WithMessage("'{PropertyName}' must be an id.");

        // An empty token is a bad link, not a bad request: the handler rejects it like any other.
        RuleFor(command => command.Token).NotNull();
    }
}
