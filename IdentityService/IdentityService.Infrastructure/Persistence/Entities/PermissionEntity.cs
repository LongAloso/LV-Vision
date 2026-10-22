using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("permissions")]
public class PermissionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [MaxLength(150)]
    public string Code { get; set; } = default!;

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = new List<RolePermissionEntity>();
}
