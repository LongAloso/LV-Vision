using System.Data;
using System.Text.Json;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Validation;

public sealed class UserSettingReferenceValidator : IUserSettingReferenceValidator
{
    private readonly IdentityDbContext _db;

    public UserSettingReferenceValidator(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task ValidateAsync(
        IReadOnlyCollection<Guid>? partIds,
        IReadOnlyCollection<Guid>? stationIds,
        CancellationToken cancellationToken = default)
    {
        var distinctPartIds = (partIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        var distinctStationIds = (stationIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (distinctPartIds.Length == 0 && distinctStationIds.Length == 0) return;

        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COUNT_BIG(*)
                FROM OPENJSON(@partIds) token
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.parts part
                    WHERE part.id = TRY_CONVERT(uniqueidentifier, token.value));

                SELECT COUNT_BIG(*)
                FROM OPENJSON(@stationIds) token
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.stations station
                    WHERE station.id = TRY_CONVERT(uniqueidentifier, token.value));
                """;
            command.Parameters.Add(new SqlParameter("@partIds", SqlDbType.NVarChar, -1)
            {
                Value = JsonSerializer.Serialize(distinctPartIds)
            });
            command.Parameters.Add(new SqlParameter("@stationIds", SqlDbType.NVarChar, -1)
            {
                Value = JsonSerializer.Serialize(distinctStationIds)
            });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var missingParts = reader.GetInt64(0);
            await reader.NextResultAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var missingStations = reader.GetInt64(0);
            if (missingParts > 0 || missingStations > 0)
            {
                throw new InvalidOperationException(
                    $"User setting contains unknown references (parts={missingParts}, stations={missingStations}).");
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
