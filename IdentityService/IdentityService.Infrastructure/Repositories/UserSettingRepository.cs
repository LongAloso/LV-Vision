using IdentityService.Application.DTOs.UserSettings;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Infrastructure.Repositories
{
    public class UserSettingRepository : IUserSettingRepository
    {
        private readonly IdentityDbContext _dbContext;

        public UserSettingRepository(IdentityDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        // 1. CREATE
        public async Task CreateAsync(UserSetting userSetting, CancellationToken cancellationToken = default)
        {
            var entity = new UserSettingEntity
            {
                Id = userSetting.Id == Guid.Empty ? Guid.NewGuid() : userSetting.Id,
                UserId = userSetting.UserId,
                DisplayDefault = userSetting.DisplayDefault,
                MeasurementType = userSetting.MeasurementType,
                StationIds = userSetting.StationIds,
                PartIds = userSetting.PartIds,
                LineSwitchInterval = userSetting.LineSwitchInterval,
                PartSwitchInterval = userSetting.PartSwitchInterval,
                PageFlipInterval = userSetting.PageFlipInterval,
                IsImportant = userSetting.IsImportant,
                IsShowAlarm = userSetting.IsShowAlarm,
                TimeRangeChart = userSetting.TimeRangeChart,
                GridChartOption = userSetting.GridChartOption?.Trim(),
                CharacteristicIds = userSetting.CharacteristicIds != null ? string.Join(",", userSetting.CharacteristicIds) : null,
                StartDate = userSetting.StartDate,
                EndDate = userSetting.EndDate,
                IsEnabled = userSetting.IsEnabled,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            await _dbContext.UserSettings.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. UPDATE
        public async Task UpdateAsync(UserSetting userSetting, CancellationToken cancellationToken = default)
        {
            var existingSetting = await _dbContext.UserSettings
                .FirstOrDefaultAsync(x => x.Id == userSetting.Id, cancellationToken);

            if (existingSetting == null) throw new Exception("User setting not found");

            existingSetting.DisplayDefault = userSetting.DisplayDefault;
            existingSetting.MeasurementType = userSetting.MeasurementType;
            existingSetting.StationIds = userSetting.StationIds;
            existingSetting.PartIds = userSetting.PartIds;
            existingSetting.LineSwitchInterval = userSetting.LineSwitchInterval;
            existingSetting.PartSwitchInterval = userSetting.PartSwitchInterval;
            existingSetting.PageFlipInterval = userSetting.PageFlipInterval;
            existingSetting.TimeRangeChart = userSetting.TimeRangeChart;
            existingSetting.IsImportant = userSetting.IsImportant;
            existingSetting.IsShowAlarm = userSetting.IsShowAlarm;
            existingSetting.GridChartOption = userSetting.GridChartOption?.Trim();
            existingSetting.CharacteristicIds = userSetting.CharacteristicIds != null ? string.Join(",", userSetting.CharacteristicIds) : null;
            existingSetting.StartDate = userSetting.StartDate;
            existingSetting.EndDate = userSetting.EndDate;
            existingSetting.IsEnabled = userSetting.IsEnabled;
            existingSetting.UpdatedAtUtc = DateTimeOffset.UtcNow;

            _dbContext.UserSettings.Update(existingSetting);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 3. DELETE
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.UserSettings
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity != null)
            {
                _dbContext.UserSettings.Remove(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<UserSetting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.UserSettings
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            return entity is null ? null : MapToDomain(entity);
        }
        // 4. SEARCH
        public async Task<IReadOnlyList<UserSetting>> SearchAsync(string userId, string? keyword, CancellationToken cancellationToken = default)
        {

                if (!Guid.TryParse(userId, out var idUser))
                    return Array.Empty<UserSetting>();

                var query = _dbContext.UserSettings
                    .AsNoTracking()
                    .Where(x => x.UserId == idUser);

                if (!string.IsNullOrEmpty(keyword))
                {
                    switch (keyword.ToLower())
                    {
                        case "pagelayout":
                            query = query.Where(x => x.DisplayDefault.HasValue && x.DisplayDefault.Value);
                            break;
                        case "measureform":
                            query = query.Where(x => x.MeasurementType.HasValue);
                            break;
                        case "dashboardspc":
                            query = query.Where(x =>
                                x.StationIds != null &&
                                x.StationIds.Any() &&
                                x.PartIds != null &&
                                x.PartIds.Any());
                            break;
                    }
                }
                var entities = await query.ToListAsync(cancellationToken);

                // 2. Map từng Entity sang Domain Object bằng hàm MapToDomain đã có
                return entities.Select(MapToDomain).ToList();

        }
        private static UserSetting MapToDomain(UserSettingEntity entity) => new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            DisplayDefault = entity.DisplayDefault,
            MeasurementType = entity.MeasurementType,
            StationIds = entity.StationIds,
            PartIds = entity.PartIds,
            LineSwitchInterval = entity.LineSwitchInterval,
            PartSwitchInterval = entity.PartSwitchInterval,
            PageFlipInterval = entity.PageFlipInterval,
            IsImportant = entity.IsImportant,
            IsShowAlarm = entity.IsShowAlarm,
            TimeRangeChart = entity.TimeRangeChart,
            GridChartOption = entity.GridChartOption?.Trim(),
            CharacteristicIds = entity.CharacteristicIds,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsEnabled = entity.IsEnabled
        };
    }
}
