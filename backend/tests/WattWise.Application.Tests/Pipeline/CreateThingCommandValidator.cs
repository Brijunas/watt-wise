using FluentValidation;

namespace WattWise.Application.Tests.Pipeline;

public sealed class CreateThingCommandValidator : AbstractValidator<CreateThingCommand>
{
    public CreateThingCommandValidator()
    {
        RuleFor(command => command.Address.StreetName).NotEmpty();
    }
}
