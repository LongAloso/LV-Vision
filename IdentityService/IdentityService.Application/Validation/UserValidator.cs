using FluentValidation;
using IdentityService.Application.DTOs.Users;

namespace IdentityService.Application.Validation;

/// <summary>
/// Validator cho CreateUserRequestDto
/// </summary>
public class CreateUserValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("UserName is required")
            .Length(3, 50).WithMessage("UserName must be between 3 and 50 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email format is invalid")
            .Length(5, 256).WithMessage("Email must be between 5 and 256 characters");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required")
            .Length(2, 256).WithMessage("Full Name must be between 2 and 256 characters");

        RuleFor(x => x.Password)
            .ApplyStandardPasswordRules()
            .Must((req, p) => !string.Equals(p, req.UserName, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Password must not match the username.");

        RuleFor(x => x.Roles)
            .NotNull().WithMessage("Roles list cannot be null");

        RuleFor(x => x.Departments)
            .NotNull().WithMessage("Departments list cannot be null");

        RuleFor(x => x.PhoneNumber)
           .Matches(@"^(\+|0)[1-9]\d{7,14}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Number invalid. Please enter in international format (+84...) or start with 0.");
        
        RuleFor(x => x.ProfileImageUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) ||
                System.Text.RegularExpressions.Regex.IsMatch(url, @"\.(jpg|jpeg|png|gif|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
         .WithMessage("Profile image must end with .jpg, .jpeg, .png, .gif, or .webp extension.");
    }
}

/// <summary>
/// Validator cho UpdateUserRequestDto
/// </summary>
public class UpdateUserValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email format is invalid")
            .Length(5, 256).WithMessage("Email must be between 5 and 256 characters");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required")
            .Length(2, 256).WithMessage("Full Name must be between 2 and 256 characters");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .ApplyStandardPasswordRules()
            .When(x => !string.IsNullOrWhiteSpace(x.Password));

        RuleFor(x => x.Roles)
            .NotNull().WithMessage("Roles list cannot be null");

        RuleFor(x => x.Departments)
            .NotNull().WithMessage("Departments list cannot be null");

        RuleFor(x => x.PhoneNumber)
          .Matches(@"^(\+|0)[1-9]\d{7,14}$")
           .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
           .WithMessage("Phonenumber Valid. Please enter international format (+84...) or start with 0.");
        
        RuleFor(x => x.ProfileImageUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) ||
                System.Text.RegularExpressions.Regex.IsMatch(url, @"\.(jpg|jpeg|png|gif|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .WithMessage("Profile image must end with .jpg, .jpeg, .png, .gif, or .webp extension.");
    }
}
