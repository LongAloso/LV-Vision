using FluentAssertions;
using IdentityService.Domain.Security;
using Xunit;

namespace IdentityService.Tests;

public class SpcPermissionsTests
{
    [Fact]
    public void PermissionManagementConstants_Should_NotAlias_UserPermissions()
    {
        SpcPermissions.Permissions.Read.Should().Be("Permissions.Read");
        SpcPermissions.Permissions.Manage.Should().Be("Permissions.Manage");
    }

    [Fact]
    public void CanonicalPermissions_Should_Match_Current_Config_And_Gage_Policies()
    {
        SpcPermissions.Canonical.Should().Contain(new[]
        {
            "Parts.Manage",
            "Characteristics.Delete",
            "Gages.Manage",
            "Gages.Calibrations.Manage",
            "Gages.Studies.Manage"
        });

        SpcPermissions.Canonical.Should().OnlyHaveUniqueItems();
        SpcPermissions.Canonical.Should().NotContain(x => x.StartsWith("Spc.", StringComparison.Ordinal));
        SpcPermissions.Canonical.Should().NotContain(x => x != x.Trim());
    }

    [Fact]
    public void OeePermissions_Should_BeCanonical_AndModeratorGetsViewOnly()
    {
        SpcPermissions.Canonical.Should().Contain(new[]
        {
            SpcPermissions.Oee.View,
            SpcPermissions.Oee.DowntimeManage,
            SpcPermissions.Oee.ConfigManage
        });
        SpcPermissions.ModeratorRead.Should().Contain(SpcPermissions.Oee.View);
        SpcPermissions.ModeratorRead.Should().NotContain(SpcPermissions.Oee.DowntimeManage);
        SpcPermissions.ModeratorRead.Should().NotContain(SpcPermissions.Oee.ConfigManage);
    }
}
