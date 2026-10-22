using FluentValidation;
using IdentityService.Application.DTOs.Permissions;

namespace IdentityService.Application.Validation;

/// <summary>
/// Validator cho CreatePermissionRequestDto
/// </summary>
public class CreatePermissionValidator : AbstractValidator<CreatePermissionRequestDto>
{
    public CreatePermissionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Permission Name is required")
            .Length(2, 100).WithMessage("Permission Name must be between 2 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
    }
}

/// <summary>
/// Validator cho UpdatePermissionRequestDto (nếu có)
/// </summary>
public class UpdatePermissionValidator : AbstractValidator<UpdatePermissionRequestDto>
{
    public UpdatePermissionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Permission Name is required")
            .Length(2, 100).WithMessage("Permission Name must be between 2 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
    }
}
