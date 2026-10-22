using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("user_roles")]
public class UserRoleEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role_id")]
    public Guid RoleId { get; set; }

    public UserEntity User { get; set; } = default!;
    public RoleEntity Role { get; set; } = default!;
}
