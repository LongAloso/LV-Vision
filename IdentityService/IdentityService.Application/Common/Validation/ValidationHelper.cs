using FluentValidation;

namespace IdentityService.Application.Common.Validation;

/// <summary>
/// Base helper class for manual FluentValidation
/// Dùng chung cho tất cả services để validate request DTO
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validate async và throw ValidationException nếu không hợp lệ
    /// </summary>
    /// <typeparam name="T">Type của DTO cần validate</typeparam>
    /// <param name="validator">Validator instance</param>
    /// <param name="request">Request DTO</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ValidationException">Ném khi validation failed</exception>
    public static async Task ValidateAsync<T>(
        this IValidator<T> validator,
        T request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }
}
