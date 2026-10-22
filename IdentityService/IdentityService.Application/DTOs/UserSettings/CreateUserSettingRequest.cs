using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Application.DTOs.UserSettings
{
    public class CreateUserSettingRequest
    {
        public string? UserName { get; set; }
        public Guid UserId { get; set; }      // dbo.user_settings.user_id
        public bool? DisplayDefault { get; set; } // dbo.user_settings.display_default default hiển thị danh sách dạng list/grid

        public byte? MeasurementType { get; set; } = default!;

        public List<Guid>? StationIds { get; set; }      // dbo.user_settings.station_id

        public List<Guid>? PartIds { get; set; } // dbo.user_settings.part_id

        public int? LineSwitchInterval { get; set; } = 60;// dbo.user_settings.line_switch_interval default thời gian chuyển line (đơn vị phút)

        public int? PartSwitchInterval { get; set; } = 40;// dbo.user_settings.part_switch_interval default thời gian chuyển part (đơn vị phút)

        public int? PageFlipInterval { get; set; } = 10; // dbo.user_settings.page_switch_interval default thời gian chuyển page (đơn vị phút)

        public string? GridChartOption { get; set; } = "3x3";// dbo.user_settings.grid_chart_option default hiển thị bố cục chart 3x3 trang SPC dashboard

        public List<Guid>? CharacteristicIds { get; set; } = new List<Guid>(); // dbo.user_settings.characteristic_ids default hiển thị những char nào trong chart SPC dashboard

        public DateTimeOffset? StartDate { get; set; } = DateTimeOffset.UtcNow.AddDays(-10); // dbo.user_settings.start_date default ngày bắt đầu hiển thị dữ liệu trong chart SPC dashboard

        public DateTimeOffset? EndDate { get; set; } = DateTimeOffset.UtcNow; // dbo.user_settings.start_date default ngày kết thúc hiển thị dữ liệu trong chart SPC dashboard

        public bool IsEnabled { get; set; } = true;
        public bool? IsImportant { get; set; } = true;
        public bool? IsShowAlarm { get; set; } = true;
        public int? TimeRangeChart { get; set; } = 24; // dbo.user_settings.time_range_chart default lấy dữ liệu trong khoảng thời gian (giờ) để hiển thị trong chart SPC dashboard



        public int EventType { get; set; } = 0;

        public bool ViaWeb { get; set; } = true;

        public bool ViaEmail { get; set; } = false;

        public bool ViaSms { get; set; } = false;
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    }
}
