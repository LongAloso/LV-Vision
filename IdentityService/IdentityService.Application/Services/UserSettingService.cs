using IdentityService.Application.DTOs.UserSettings;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Application.Services
{
    public class UserSettingService
    {
        private readonly IUserSettingRepository _userSettingRepository;
        readonly IUserRepository _userRepository;
        private readonly IUserSettingReferenceValidator _referenceValidator;
        public UserSettingService(
            IUserSettingRepository userSettingRepository,
            IUserRepository userRepository,
            IUserSettingReferenceValidator referenceValidator)
        {
            _userSettingRepository = userSettingRepository;
            _userRepository = userRepository;
            _referenceValidator = referenceValidator;
        }

        public async Task<UserSettingDto> CreateAsync(CreateUserSettingRequest request, CancellationToken ct = default)
        {
            await _referenceValidator.ValidateAsync(request.PartIds, request.StationIds, ct);
            request.PartIds = request.PartIds?.Distinct().ToList();
            request.StationIds = request.StationIds?.Distinct().ToList();
            if (request.UserName?.Length > 0)
            {
                var userid = await _userRepository.GetUserIdByUserNameAsync(request.UserName!, ct);
                request.UserId = userid.HasValue ? userid.Value : request.UserId;
            }
            // Kiểm tra xem User đã có setting chưa (thường mỗi user chỉ có 1 bản ghi setting)
            var existing = await _userSettingRepository.SearchAsync(request.UserId.ToString(),null, ct);
            
            var setting = new UserSetting();
            if (existing is not null && existing?.Count > 0)
            {
                setting = new UserSetting
                {
                    Id = existing.First().Id,
                    UserId = request.UserId,
                    DisplayDefault = request.DisplayDefault,
                    MeasurementType = request.MeasurementType,
                    StationIds = request.StationIds,
                    PartIds = request.PartIds,
                    LineSwitchInterval = request.LineSwitchInterval,
                    PartSwitchInterval = request.PartSwitchInterval,
                    PageFlipInterval = request.PageFlipInterval,
                    IsImportant = request.IsImportant,
                    IsShowAlarm = request.IsShowAlarm,
                    TimeRangeChart = request.TimeRangeChart,
                    GridChartOption = request.GridChartOption?.Trim(),
                    // Chuyển List<Guid> sang String để lưu DB
                    CharacteristicIds = request.CharacteristicIds != null ? string.Join(",", request.CharacteristicIds) : null,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    IsEnabled = request.IsEnabled,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                await _userSettingRepository.UpdateAsync(setting, ct);
            }
            else
            {
                 setting = new UserSetting
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    DisplayDefault = request.DisplayDefault,
                    MeasurementType = request.MeasurementType,
                    StationIds = request.StationIds,
                    PartIds = request.PartIds,
                    LineSwitchInterval = request.LineSwitchInterval,
                    PartSwitchInterval = request.PartSwitchInterval,
                    PageFlipInterval = request.PageFlipInterval,
                     IsImportant = request.IsImportant,
                     IsShowAlarm = request.IsShowAlarm,
                     GridChartOption = request.GridChartOption?.Trim(),
                     TimeRangeChart = request.TimeRangeChart,
                     // Chuyển List<Guid> sang String để lưu DB
                     CharacteristicIds = request.CharacteristicIds != null ? string.Join(",", request.CharacteristicIds) : null,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    IsEnabled = request.IsEnabled,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                await _userSettingRepository.CreateAsync(setting, ct);
            }
            return MapToDto(setting);
        }

        public async Task<UserSettingDto?> UpdateAsync(Guid id, UpdateUserSettingRequest request, Guid? ownerUserId = null, CancellationToken ct = default)
        {
            await _referenceValidator.ValidateAsync(request.PartIds, request.StationIds, ct);
            request.PartIds = request.PartIds?.Distinct().ToList();
            request.StationIds = request.StationIds?.Distinct().ToList();
            var setting = await _userSettingRepository.GetByIdAsync(id, ct);
            if (setting is null) return null;
            if (ownerUserId.HasValue && setting.UserId != ownerUserId.Value)
                throw new UnauthorizedAccessException("The user setting does not belong to the current user.");

            setting.DisplayDefault = request.DisplayDefault;
            setting.MeasurementType = request.MeasurementType;
            setting.StationIds = request.StationIds;
            setting.PartIds = request.PartIds;
            setting.LineSwitchInterval = request.LineSwitchInterval;
            setting.PartSwitchInterval = request.PartSwitchInterval;
            setting.PageFlipInterval = request.PageFlipInterval;
            setting.IsImportant = request.IsImportant;
            setting.IsShowAlarm = request.IsShowAlarm;
            setting.TimeRangeChart = request.TimeRangeChart;
            setting.GridChartOption = request.GridChartOption?.Trim();
            setting.CharacteristicIds = request.CharacteristicIds != null ? string.Join(",", request.CharacteristicIds) : null;
            setting.StartDate = request.StartDate;
            setting.EndDate = request.EndDate;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _userSettingRepository.UpdateAsync(setting, ct);
            return MapToDto(setting);
        }

        public async Task<bool> DeleteAsync(Guid id, Guid? ownerUserId = null, CancellationToken ct = default)
        {
            var setting = await _userSettingRepository.GetByIdAsync(id, ct);
            if (setting is null) return false;
            if (ownerUserId.HasValue && setting.UserId != ownerUserId.Value)
                throw new UnauthorizedAccessException("The user setting does not belong to the current user.");

            await _userSettingRepository.DeleteAsync(id, ct);
            return true;
        }

        public async Task<IReadOnlyList<UserSettingDto>> GetAllAsync(UserSettingsRequest rq, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rq.UserId))
                return Array.Empty<UserSettingDto>();

            var settings = await _userSettingRepository.SearchAsync(rq.UserId, rq.keyword, ct);
            return settings.Select(MapToDto).ToList();
        }

        // Hàm chuyển đổi từ Entity sang DTO
        private static UserSettingDto MapToDto(UserSetting entity) => new()
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
            GridChartOption = entity.GridChartOption?.Trim(),
            IsImportant = entity.IsImportant,
            IsShowAlarm = entity.IsShowAlarm,
            TimeRangeChart = entity.TimeRangeChart,
            // Chuyển ngược từ String trong DB sang List<Guid> cho UI
            CharacteristicIds = !string.IsNullOrEmpty(entity.CharacteristicIds)
                ? entity.CharacteristicIds.Split(',').Select(Guid.Parse).ToList()
                : new List<Guid>(),
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsEnabled = entity.IsEnabled
        };
    }
}
