namespace IdentityService.Application.Abstractions;

public interface IUserSettingReferenceValidator
{
    Task ValidateAsync(
        IReadOnlyCollection<Guid>? partIds,
        IReadOnlyCollection<Guid>? stationIds,
        CancellationToken cancellationToken = default);
}
