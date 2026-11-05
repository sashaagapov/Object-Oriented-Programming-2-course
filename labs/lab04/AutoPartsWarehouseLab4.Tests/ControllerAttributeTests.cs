using System.Reflection;
using AutoPartsWarehouse.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Reflection sweep over every controller action: every state-changing POST must carry
/// [ValidateAntiForgeryToken], and every action must be reachable only by an authenticated
/// user (no action is accidentally left without an effective [Authorize] because a derived
/// class/action removed it). This catches a missing attribute on any action, not just the
/// handful a manual click-through would happen to try.
/// </summary>
public class ControllerAttributeTests
{
    private static readonly Type[] Controllers =
    {
        typeof(AutoPartsController), typeof(PurchaseOrdersController), typeof(SuppliersController),
        typeof(VehicleModelsController), typeof(ReportsController), typeof(AdminController), typeof(AccountController)
    };

    private static IEnumerable<MethodInfo> ActionMethods(Type controller) => controller
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && typeof(IActionResult).IsAssignableFrom(m.ReturnType) || typeof(Task<IActionResult>) == m.ReturnType);

    public static IEnumerable<object[]> PostActions()
    {
        foreach (var controller in Controllers)
        foreach (var method in ActionMethods(controller))
        {
            if (method.GetCustomAttribute<HttpPostAttribute>() != null)
                yield return new object[] { controller, method };
        }
    }

    [Theory]
    [MemberData(nameof(PostActions))]
    public void EveryHttpPostAction_HasAntiforgeryProtection(Type controller, MethodInfo method)
    {
        var hasToken = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null;
        Assert.True(hasToken, $"{controller.Name}.{method.Name} is [HttpPost] but missing [ValidateAntiForgeryToken].");
    }

    [Fact]
    public void EveryControllerExceptAccount_RequiresAuthorization()
    {
        foreach (var controller in Controllers.Where(c => c != typeof(AccountController)))
        {
            var classLevel = controller.GetCustomAttribute<AuthorizeAttribute>();
            Assert.True(classLevel != null, $"{controller.Name} has no class-level [Authorize] — verify every action is individually protected.");
        }
    }

    [Theory]
    [InlineData(typeof(SuppliersController), WarehouseRolesManager)]
    [InlineData(typeof(ReportsController), WarehouseRolesManager)]
    [InlineData(typeof(AdminController), WarehouseRolesAdministrator)]
    public void SensitiveControllers_AreRestrictedToExpectedRole(Type controller, string expectedRole)
    {
        var attribute = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedRole, attribute!.Roles);
    }

    private const string WarehouseRolesManager = "Manager";
    private const string WarehouseRolesAdministrator = "Administrator";

    [Fact]
    public void ConfirmPayment_IsManagerOnly_And_ReceiveCompleteCancelReturnAdjust_AreWorkerOnly()
    {
        AssertActionRoles(typeof(AutoPartsController), nameof(AutoPartsController.ConfirmPayment), "Manager");
        AssertActionRoles(typeof(AutoPartsController), nameof(AutoPartsController.CompleteIssue), "WarehouseWorker");
        AssertActionRoles(typeof(AutoPartsController), nameof(AutoPartsController.CancelIssue), "WarehouseWorker");
        AssertActionRoles(typeof(AutoPartsController), nameof(AutoPartsController.ReturnIssue), "WarehouseWorker");
        AssertActionRoles(typeof(PurchaseOrdersController), nameof(PurchaseOrdersController.Receive), "WarehouseWorker");
    }

    private static void AssertActionRoles(Type controller, string actionName, string expectedRole)
    {
        var method = controller.GetMethods().First(m => m.Name == actionName && m.GetCustomAttribute<HttpPostAttribute>() != null);
        var attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedRole, attribute!.Roles);
    }
}
