using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class GrantAdminRoleCommandValidator : AbstractValidator<GrantAdminRoleCommand>
{
    public GrantAdminRoleCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
    }
}
