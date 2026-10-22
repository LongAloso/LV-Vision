using FluentValidation;
using IdentityService.Application.DTOs.Departments;

namespace IdentityService.Application.Validation;

/// <summary>
/// Validator cho CreateDepartmentRequestDto
/// </summary>
public class CreateDepartmentValidator : AbstractValidator<CreateDepartmentRequestDto>
{
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Department Code is required")
            .Length(1, 50).WithMessage("Department Code must be between 1 and 50 characters")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Department Code can only contain letters, numbers, underscores, and hyphens");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department Name is required")
            .Length(2, 200).WithMessage("Department Name must be between 2 and 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.ParentId)
            .MaximumLength(36).WithMessage("Parent ID must be valid GUID format");
    }
}

/// <summary>
/// Validator cho UpdateDepartmentRequestDto
/// </summary>
public class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentRequestDto>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department Name is required")
            .Length(2, 200).WithMessage("Department Name must be between 2 and 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.ParentId)
            .MaximumLength(36).WithMessage("Parent ID must be valid GUID format");
    }
}
