using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("user_departments")]
public class UserDepartmentEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("department_id")]
    public Guid DepartmentId { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("assigned_at_utc")]
    public DateTimeOffset AssignedAtUtc { get; set; }

    public UserEntity User { get; set; } = default!;
    public DepartmentEntity Department { get; set; } = default!;
}
