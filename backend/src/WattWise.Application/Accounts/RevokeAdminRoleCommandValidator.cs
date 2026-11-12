using FluentValidation;

namespace WattWise.Application.Accounts;

public sealed class RevokeAdminRoleCommandValidator : AbstractValidator<RevokeAdminRoleCommand>
{
    public RevokeAdminRoleCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
    }
}
