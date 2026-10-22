using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Infrastructure.Persistence.Entities
{
    [Table("user_settings")]

    public class UserSettingEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("user_id")]// dbo.user_settings.id
        public Guid UserId { get; set; }      // dbo.user_settings.user_id
        [Column("display_default")]
        public bool? DisplayDefault { get; set; } // dbo.user_settings.display_default default hiển thị danh sách dạng list/grid

        [Column("measurement_type")] // default hiển thị cac char có kiểu đo def(những char đo bằng type đo này trong form)
        public byte? MeasurementType { get; set; } = default!;

        [Column("station_ids")]

        public List<Guid>? StationIds { get; set; }      // dbo.user_settings.station_ids
        [Column("part_ids")]

        public List<Guid>? PartIds { get; set; } // dbo.user_settings.part_ids
        [Column("line_flip_interval")]

        public int? LineSwitchInterval { get; set; } = 60;// dbo.user_settings.line_switch_interval default thời gian chuyển line (đơn vị phút)
        [Column("part_flip_interval")]

        public int? PartSwitchInterval { get; set; } = 40;// dbo.user_settings.part_switch_interval default thời gian chuyển part (đơn vị phút)
        [Column("page_flip_interval")]

        public int? PageFlipInterval { get; set; } = 10; // dbo.user_settings.page_switch_interval default thời gian chuyển page (đơn vị phút)
        [Column("show_important_char")]
        public bool? IsImportant { get; set; } = true;
        [Column("show_alarm")]
        public bool? IsShowAlarm { get; set; } = true;
        [Column("time_range_chart")]

        public int? TimeRangeChart { get; set; } = 24;


        [Column("grid_chart_opt")]

        public string? GridChartOption { get; set; } = "3x3";// dbo.user_settings.grid_chart_option default hiển thị bố cục chart 3x3 trang SPC dashboard
        [Column("character_ids")]

        public string? CharacteristicIds { get; set; } // dbo.user_settings.characteristic_ids default hiển thị những char nào trong chart SPC dashboard
        [Column("start_date")]

        public DateTimeOffset? StartDate { get; set; } = DateTimeOffset.UtcNow.AddDays(-10); // dbo.user_settings.start_date default ngày bắt đầu hiển thị dữ liệu trong chart SPC dashboard
        [Column("end_date")]

        public DateTimeOffset? EndDate { get; set; } = DateTimeOffset.UtcNow; // dbo.user_settings.start_date default ngày kết thúc hiển thị dữ liệu trong chart SPC dashboard
        [Column("is_enabled")]

        public bool IsEnabled { get; set; } = true;
        [Column("event_type")]

        public int EventType { get; set; } = 0;
        [Column("via_web")]

        public bool ViaWeb { get; set; } = true;
        [Column("via_email")]

        public bool ViaEmail { get; set; } = false;
        [Column("via_sms")]

        public bool ViaSms { get; set; } = false;
        [Column("created_at_utc")]

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        [Column("updated_at_utc")]

        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
