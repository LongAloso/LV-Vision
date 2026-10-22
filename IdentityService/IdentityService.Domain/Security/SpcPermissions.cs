namespace IdentityService.Domain.Security;

public static class SpcPermissions
{
    public static class Users
    {
        public const string Read = "Users.Read";
        public const string Manage = "Users.Manage";
    }
    public static class Roles
    {
        public const string Read = "Roles.Read";
        public const string Manage = "Roles.Manage";
    }
    public static class Permissions
    {
        public const string Read = "Permissions.Read";
        public const string Manage = "Permissions.Manage";
    }
    public static class Departments
    {
        public const string Read = "Departments.Read";
        public const string Manage = "Departments.Manage";
    }
    public static class CustomFields
    {
        public const string Read = "CustomFields.Read";
        public const string Manage = "CustomFields.Manage";
    }
    public static class Cameras
    {
        public const string Read = "Camera.Read";
        public const string Manage = "Camera.Manage";
    }
    public static class Alerts
    {
        public const string Read = "Alert.Read";
        public const string Manage = "Alert.Manage";
    }
    public static class Behaviors
    {
        public const string Read = "Behavior.Read";
        public const string Manage = "Behavior.Manage";
    }
    public static class Oee
    {
        public const string View = "Oee.View";
        public const string DowntimeManage = "Oee.Downtime.Manage";
        public const string ConfigManage = "Oee.Config.Manage";
    }

    /// <summary>
    /// Permission names enforced by the current Identity, Config and Gage APIs.
    /// Identity owns the permission registry, so these values intentionally mirror
    /// the public authorization contracts of those services.
    /// </summary>
    public static IReadOnlyCollection<string> Canonical { get; } = Array.AsReadOnly(new[]
    {
        Users.Read,
        Users.Manage,
        Roles.Read,
        Roles.Manage,
        Permissions.Read,
        Permissions.Manage,
        Departments.Read,
        Departments.Manage,
        CustomFields.Read,
        CustomFields.Manage,
        Cameras.Read,
        Cameras.Manage,
        Alerts.Read,
        Alerts.Manage,
        Behaviors.Read,
        Behaviors.Manage,
        "Parts.Read",
        "Parts.Manage",
        "Operations.Read",
        "Operations.Manage",
        "Characteristics.Read",
        "Characteristics.Manage",
        "Characteristics.Delete",
        "Stations.Read",
        "Stations.Manage",
        "InspectionPlans.Read",
        "InspectionPlans.Manage",
        "InspectionPlans.Delete",
        "DocumentGuides.Read",
        "DocumentGuides.Manage",
        "Lookups.Read",
        "Lookups.Manage",
        "Gages.Read",
        "Gages.Manage",
        "Gages.Calibrations.Manage",
        "Gages.Studies.Manage",
        Oee.View,
        Oee.DowntimeManage,
        Oee.ConfigManage
    });

    /// <summary>
    /// Legacy permission names remain registered during the transition so tokens,
    /// saved roles and older clients are not broken. New policies use Canonical.
    /// </summary>
    public static IReadOnlyCollection<string> Legacy { get; } = Array.AsReadOnly(new[]
    {
        "Users.Create", "Users.Update", "Users.Delete",
        "Roles.Create", "Roles.Update", "Roles.Delete",
        "Permissions.Create", "Permissions.Update", "Permissions.Delete",
        "Departments.Create", "Departments.Update", "Departments.Delete",
        "Spc.Parts.Read", "Spc.Parts.Create", "Spc.Parts.Update", "Spc.Parts.Delete",
        "Spc.Operations.Read", "Spc.Operations.Create", "Spc.Operations.Update", "Spc.Operations.Delete",
        "Spc.Stations.Read", "Spc.Stations.Create", "Spc.Stations.Update", "Spc.Stations.Delete",
        "Spc.Characteristics.Read", "Spc.Characteristics.Create", "Spc.Characteristics.Update", "Spc.Characteristics.Delete",
        "Spc.Gages.Read", "Spc.Gages.Create", "Spc.Gages.Update", "Spc.Gages.Delete",
        "Spc.GageStudies.Read", "Spc.GageStudies.Create", "Spc.GageStudies.Update", "Spc.GageStudies.Delete",
        "Spc.GageCalibrations.Read", "Spc.GageCalibrations.Create", "Spc.GageCalibrations.Update", "Spc.GageCalibrations.Delete"
    });

    public static IReadOnlyCollection<string> ModeratorRead { get; } = Array.AsReadOnly(new[]
    {
        Users.Read,
        Roles.Read,
        Permissions.Read,
        Departments.Read,
        Cameras.Read,
        Alerts.Read,
        Behaviors.Read,
        "Parts.Read",
        "Operations.Read",
        "Characteristics.Read",
        "Stations.Read",
        "InspectionPlans.Read",
        "DocumentGuides.Read",
        "Lookups.Read",
        "Gages.Read",
        Oee.View
    });
}
