using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace IdentityService.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();
    public DbSet<UserDepartmentEntity> UserDepartments => Set<UserDepartmentEntity>();
    public DbSet<CustomFieldDefinitionEntity> CustomFieldDefinitions => Set<CustomFieldDefinitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keep schema consistent with existing migrations.

        // User
        modelBuilder.Entity<UserEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserName).IsUnique();
            b.HasIndex(x => x.NormalizedEmail).IsUnique();
            b.Property(x => x.AuthzVersion).HasDefaultValue(1L);
        });

        // Role
        modelBuilder.Entity<RoleEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.NormalizedName).IsUnique();
        });

        // Permission
        modelBuilder.Entity<PermissionEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
        });

        // UserRole
        modelBuilder.Entity<UserRoleEntity>(b =>
        {
            b.HasKey(x => new { x.UserId, x.RoleId });
            b.HasOne(x => x.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RolePermission
        modelBuilder.Entity<RolePermissionEntity>(b =>
        {
            b.HasKey(x => new { x.RoleId, x.PermissionId });

            b.HasOne(x => x.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshTokenEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => new { x.UserId, x.SessionId });

            b.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Department
        modelBuilder.Entity<DepartmentEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();

            b.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId);
        });

        // UserDepartment
        modelBuilder.Entity<UserDepartmentEntity>(b =>
        {
            b.HasKey(x => new { x.UserId, x.DepartmentId });
            b.HasIndex(x => x.DepartmentId);

            b.HasOne(x => x.User)
                .WithMany(u => u.UserDepartments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Department)
                .WithMany(d => d.UserDepartments)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomFieldDefinitions
        modelBuilder.Entity<CustomFieldDefinitionEntity>(b =>
        {
            b.ToTable("custom_field_definitions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
            b.Property(x => x.Label).HasColumnName("label").HasMaxLength(200);
            b.Property(x => x.DataType).HasColumnName("data_type");
            b.Property(x => x.IsRequired).HasColumnName("is_required");
            b.Property(x => x.IsActive).HasColumnName("is_active");
            // Provider-agnostic JSON storage (works with Postgres + SQL Server + others).
            // - Postgres: store as TEXT (you can later migrate to jsonb if you need JSON indexing)
            // - SQL Server: store as NVARCHAR(MAX)
            var jsonTextColumnType = Database.IsNpgsql() ? "text" : (Database.IsSqlServer() ? "nvarchar(max)" : null);

            var enumOptionsConverter = new ValueConverter<List<string>?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

            var enumOptionsComparer = new ValueComparer<List<string>?>(
                (a, b2) => a == null && b2 == null || (a != null && b2 != null && a.SequenceEqual(b2)),
                v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s == null ? 0 : s.GetHashCode())),
                v => v == null ? null : v.ToList());

            b.Property(x => x.EnumOptions)
                .HasColumnName("enum_options")
                .HasConversion(enumOptionsConverter)
                .Metadata.SetValueComparer(enumOptionsComparer);

            if (!string.IsNullOrWhiteSpace(jsonTextColumnType))
                b.Property(x => x.EnumOptions).HasColumnType(jsonTextColumnType);
            b.Property(x => x.MinValue).HasColumnName("min_value");
            b.Property(x => x.MaxValue).HasColumnName("max_value");
            b.Property(x => x.RegexPattern).HasColumnName("regex_pattern").HasMaxLength(500);
            var defaultValueConverter = new ValueConverter<JsonElement?, string?>(
                v => v.HasValue ? v.Value.GetRawText() : null,
                v => string.IsNullOrWhiteSpace(v) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(v, (JsonSerializerOptions?)null));

            var defaultValueComparer = new ValueComparer<JsonElement?>(
                (a, b2) => a.HasValue == b2.HasValue && (!a.HasValue || a.Value.GetRawText() == b2!.Value.GetRawText()),
                v => v.HasValue ? v.Value.GetRawText().GetHashCode() : 0,
                v => v);

            b.Property(x => x.DefaultValue)
                .HasColumnName("default_value")
                .HasConversion(defaultValueConverter)
                .Metadata.SetValueComparer(defaultValueComparer);

            if (!string.IsNullOrWhiteSpace(jsonTextColumnType))
                b.Property(x => x.DefaultValue).HasColumnType(jsonTextColumnType);
            b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            b.HasIndex(x => new { x.EntityType, x.IsActive }).HasDatabaseName("ix_custom_fields_entity_active");
            b.HasIndex(x => new { x.EntityType, x.FieldKey }).IsUnique().HasDatabaseName("ux_custom_fields_entity_key");
        });
    }
}
