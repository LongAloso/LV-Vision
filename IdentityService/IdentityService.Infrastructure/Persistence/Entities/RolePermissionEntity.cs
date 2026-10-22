using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("role_permissions")]
public class RolePermissionEntity
{
    [Column("role_id")]
    public Guid RoleId { get; set; }

    [Column("permission_id")]
    public Guid PermissionId { get; set; }

    public RoleEntity Role { get; set; } = default!;
    public PermissionEntity Permission { get; set; } = default!;
}
