using FluentValidation;
using IdentityService.Application.DTOs.Roles;

namespace IdentityService.Application.Validation;

/// <summary>
/// Validator cho CreateRoleRequestDto
/// </summary>
public class CreateRoleValidator : AbstractValidator<CreateRoleRequestDto>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role Name is required")
            .Length(2, 100).WithMessage("Role Name must be between 2 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Permissions list cannot be null");
    }
}

/// <summary>
/// Validator cho UpdateRoleRequestDto
/// </summary>
public class UpdateRoleValidator : AbstractValidator<UpdateRoleRequestDto>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Permissions list cannot be null");
    }
}
