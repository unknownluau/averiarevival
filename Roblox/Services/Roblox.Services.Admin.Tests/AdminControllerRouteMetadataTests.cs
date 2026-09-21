using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Models.Staff;
using Roblox.Services.Admin.Controllers;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Website.Filters;
using LegacyAdminController = Roblox.Website.Controllers.AdminApiController;

namespace Roblox.Services.Admin.Tests;

public class AdminControllerRouteMetadataTests
{
    [Fact]
    public void AdminController_UsesServiceSecurityMetadata()
    {
        var attributes = typeof(AdminController).GetCustomAttributes(inherit: true);

        Assert.Contains(attributes, attribute => attribute is ApiControllerAttribute);
        Assert.Contains(attributes, attribute => attribute is InternalServiceOnlyAttribute);
        Assert.Contains(attributes, attribute => attribute is RequireRobloxSessionAttribute);
        Assert.Contains(attributes, attribute => attribute is RequireRobloxCsrfAttribute);
        Assert.Contains(attributes, attribute => attribute is AdminStaffFilterAttribute);
        Assert.Contains(attributes, attribute => attribute is AdminTwoFactorFilterAttribute);
        Assert.DoesNotContain(attributes, attribute => attribute.GetType().Namespace == "Roblox.Website.Filters");

        var route = Assert.IsType<RouteAttribute>(attributes.Single(attribute => attribute is RouteAttribute));
        Assert.Equal("/v1", route.Template);
    }

    [Fact]
    public void LegacyWebsiteAdminController_IsNoLongerDiscoveredAsController()
    {
        Assert.Contains(
            typeof(LegacyAdminController).GetCustomAttributes(inherit: true),
            attribute => attribute is NonControllerAttribute);
    }

    [Fact]
    public void MigratedController_PreservesLegacyRouteMatrixUnderV1()
    {
        var legacyRoutes = ReadRouteMatrix(typeof(LegacyAdminController));
        var migratedRoutes = ReadRouteMatrix(typeof(AdminController));

        Assert.Equal(legacyRoutes.Count, migratedRoutes.Count);
        foreach (var (action, legacyRoute) in legacyRoutes)
        {
            Assert.True(migratedRoutes.TryGetValue(action, out var migratedRoute), $"Missing migrated action {action}.");
            Assert.Equal(legacyRoute.Methods, migratedRoute.Methods);
            Assert.Equal(legacyRoute.Template, migratedRoute.Template);
        }
    }

    [Fact]
    public void MigratedController_PreservesLegacyPermissionRequirements()
    {
        var legacyPermissions = ReadPermissionMatrix<StaffFilter>(typeof(LegacyAdminController), ReadLegacyPermission);
        var migratedPermissions = ReadPermissionMatrix<AdminPermissionAttribute>(typeof(AdminController), ReadAdminPermission);

        Assert.Equal(legacyPermissions.Count, migratedPermissions.Count);
        foreach (var (action, legacyPermission) in legacyPermissions)
        {
            Assert.True(migratedPermissions.TryGetValue(action, out var migratedPermission), $"Missing migrated permission for {action}.");
            Assert.Equal(legacyPermission, migratedPermission);
        }
    }

    [Fact]
    public void TwoFactorPromptAndVerify_AreExplicitlySkippedFromAdminTwoFactor()
    {
        var controller = typeof(AdminController);

        Assert.Contains(
            controller.GetMethod(nameof(AdminController.ShowPrompt))!.GetCustomAttributes(inherit: true),
            attribute => HasAttributeType(attribute, "Roblox.Web.Infrastructure.Admin.SkipAdminTwoFactorAttribute"));
        Assert.Contains(
            controller.GetMethod(nameof(AdminController.VerifyPrompt))!.GetCustomAttributes(inherit: true),
            attribute => HasAttributeType(attribute, "Roblox.Web.Infrastructure.Admin.SkipAdminTwoFactorAttribute"));
        Assert.Contains(
            controller.GetMethod(nameof(AdminController.VerifyPrompt))!.GetCustomAttributes(inherit: true),
            attribute => HasAttributeType(attribute, "Roblox.Web.Infrastructure.Metadata.SkipRobloxCsrfAttribute"));
    }

    [Theory]
    [InlineData(nameof(AdminController.CopyAssetsFromRoblox), "asset/bulk-copy-from-roblox")]
    [InlineData(nameof(AdminController.BackportAssetsFromRoblox), "asset/bulk-backport-from-roblox")]
    public void BulkRobloxAssetCopyRoutes_UseCreateAssetCopiedFromRobloxPermission(string actionName, string routeTemplate)
    {
        var method = typeof(AdminController).GetMethod(actionName);
        Assert.NotNull(method);

        var route = Assert.IsType<HttpPostAttribute>(
            method.GetCustomAttributes(inherit: true).Single(attribute => attribute is HttpPostAttribute));
        Assert.Equal(routeTemplate, route.Template);

        var permission = Assert.IsType<AdminPermissionAttribute>(
            method.GetCustomAttributes(inherit: true).Single(attribute => attribute is AdminPermissionAttribute));
        Assert.Equal(Access.CreateAssetCopiedFromRoblox, ReadAdminPermission(permission));
    }

    [Fact]
    public void FixBuggedRendersRoute_UsesRequestAssetReRenderPermission()
    {
        var method = typeof(AdminController).GetMethod(nameof(AdminController.FixBuggedRenders));
        Assert.NotNull(method);

        var route = Assert.IsType<HttpPostAttribute>(
            method.GetCustomAttributes(inherit: true).Single(attribute => attribute is HttpPostAttribute));
        Assert.Equal("asset/fix-bugged-renders", route.Template);

        var permission = Assert.IsType<AdminPermissionAttribute>(
            method.GetCustomAttributes(inherit: true).Single(attribute => attribute is AdminPermissionAttribute));
        Assert.Equal(Access.RequestAssetReRender, ReadAdminPermission(permission));
    }

    [Fact]
    public void TelemetryRoute_HasExplicitSecurityAndPermissionMetadata()
    {
        var controller = typeof(TelemetryController);
        var attributes = controller.GetCustomAttributes(inherit: true);
        Assert.Contains(attributes, attribute => attribute is InternalServiceOnlyAttribute);
        Assert.Contains(attributes, attribute => attribute is RequireRobloxSessionAttribute);
        Assert.Contains(attributes, attribute => attribute is AdminStaffFilterAttribute);
        Assert.Contains(attributes, attribute => attribute is AdminTwoFactorFilterAttribute);

        var route = Assert.IsType<RouteAttribute>(attributes.Single(attribute => attribute is RouteAttribute));
        Assert.Equal("/v1/telemetry", route.Template);

        var method = controller.GetMethod(nameof(TelemetryController.GetDashboard))!;
        var get = Assert.IsType<HttpGetAttribute>(method.GetCustomAttributes(inherit: true).Single(attribute => attribute is HttpGetAttribute));
        Assert.Equal("dashboard", get.Template);
        var permission = Assert.IsType<AdminPermissionAttribute>(method.GetCustomAttributes(inherit: true).Single(attribute => attribute is AdminPermissionAttribute));
        Assert.Equal(Access.ViewTelemetry, ReadAdminPermission(permission));
    }

    private static Dictionary<string, RouteEntry> ReadRouteMatrix(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                Method = method,
                Route = method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().SingleOrDefault(),
            })
            .Where(entry => entry.Route != null)
            .ToDictionary(
                entry => entry.Method.Name,
                entry => new RouteEntry(
                    entry.Route!.Template ?? string.Empty,
                    entry.Route.HttpMethods.OrderBy(method => method, StringComparer.Ordinal).ToArray()));
    }

    private static Dictionary<string, Access> ReadPermissionMatrix<TAttribute>(
        Type controllerType,
        Func<TAttribute, Access> readPermission)
        where TAttribute : Attribute
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                Method = method,
                Permission = method.GetCustomAttributes(inherit: true).OfType<TAttribute>().SingleOrDefault(),
            })
            .Where(entry => entry.Permission != null)
            .ToDictionary(entry => entry.Method.Name, entry => readPermission(entry.Permission!));
    }

    private static Access ReadAdminPermission(AdminPermissionAttribute attribute)
    {
        var field = typeof(AdminPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Access)field.GetValue(attribute)!;
    }

    private static Access ReadLegacyPermission(StaffFilter attribute)
    {
        var field = typeof(StaffFilter).GetField("<permission>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Access)field.GetValue(attribute)!;
    }

    private static bool HasAttributeType(object attribute, string fullName)
    {
        return string.Equals(attribute.GetType().FullName, fullName, StringComparison.Ordinal);
    }

    private sealed record RouteEntry(string Template, IReadOnlyList<string> Methods);
}
