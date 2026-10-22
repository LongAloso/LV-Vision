using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("departments")]
public class DepartmentEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = default!;

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DepartmentEntity? Parent { get; set; }
    public ICollection<DepartmentEntity> Children { get; set; } = new List<DepartmentEntity>();
    public ICollection<UserDepartmentEntity> UserDepartments { get; set; } = new List<UserDepartmentEntity>();
}
