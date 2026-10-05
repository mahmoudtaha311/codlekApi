using System.Reflection;
using System.Security.Claims;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Codlek.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// عناوين الفحوص وسياساتها.
///
/// <para>🔴 <b>تلات مستويات هنا:</b> القايمة والفحص الواحد بلا سياسة
/// (الفني بيشوف شغله هو، والتضييق جوّه الاستعلام) · المسح والتعديلات
/// <c>ManagerOrAbove</c> · الاسترجاع <c>OwnerOnly</c>.</para>
///
/// <para>⚠️ <b>والفرق ده مابيظهرش في أي فحص منطق:</b> سياسة على
/// الكلاس والفني بياخد <c>403</c> على أول صفحة بيفتحها؛ وشيلها من
/// الاسترجاع والمدير اللي مسح هو اللي بيرجّع.</para>
/// </summary>
public class ReportsRouteGuardTests
{
    private static HttpMethodAttribute Route(string action) =>
        typeof(ReportsController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

    private static string? Policy(string action) =>
        typeof(ReportsController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault()?.Policy;

    /// <summary>🔴 المسارات دي بالحرف — الداش بورد بتنديها كده.</summary>
    [Theory]
    [InlineData(nameof(ReportsController.List), "GET", "")]
    [InlineData(nameof(ReportsController.Detail), "GET", "{id:guid}")]
    [InlineData(nameof(ReportsController.Edits), "GET", "{id:guid}/edits")]
    [InlineData(nameof(ReportsController.Delete), "POST", "{id:guid}/delete")]
    [InlineData(nameof(ReportsController.Restore), "POST", "{id:guid}/restore")]
    public void The_urls_are_frozen(string action, string verb, string template)
    {
        var route = Route(action);

        Assert.Equal([verb], route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    [Theory]
    [InlineData(nameof(ReportsController.List), null)]
    [InlineData(nameof(ReportsController.Detail), null)]
    [InlineData(nameof(ReportsController.Edits), Policies.ManagerOrAbove)]
    [InlineData(nameof(ReportsController.Delete), Policies.ManagerOrAbove)]
    [InlineData(nameof(ReportsController.Restore), Policies.OwnerOnly)]
    public void The_policy_matrix_is_frozen(string action, string? policy) =>
        Assert.Equal(policy, Policy(action));

    /// <summary>
    /// ⚠️ <b>حارس الكلاس «مسجّل دخول» وبس، والبادئة ثابتة.</b> أي سياسة
    /// على الكلاس بتتجمع بـ«و» مع سياسة الإجراء — فـ<c>ManagerOrAbove</c>
    /// هنا كانت هتمنع الفني من فحوصاته هو.
    /// </summary>
    [Fact]
    public void The_class_level_gate_carries_no_policy()
    {
        var onClass = typeof(ReportsController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Null(onClass.Policy);
        Assert.Null(onClass.Roles);

        var route = typeof(ReportsController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        Assert.Equal("api/v1/reports", route.Template);
    }

    [Fact]
    public void No_reports_action_is_left_out_of_this_matrix()
    {
        int actions = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

        Assert.Equal(5, actions);
    }

    // =================================================================
    //  مين بيعدّي كل حاجز — بالسياسات الحقيقية
    // =================================================================

    private static readonly IAuthorizationService Authorization =
        new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(o => o.AddCodlekPolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

    private static async Task<bool> Allowed(UserRole role, string action)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role.ToString())], "test"));

        return (await Authorization.AuthorizeAsync(user, null, Policy(action)!)).Succeeded;
    }

    /// <summary>
    /// 🔴 <b>من القديم بالحرف:</b> المسح والتعديلات لمدير المخزن ومدير
    /// الدور والمالك؛ الاسترجاع للمالك بس. والمحاسب والفني لأ في
    /// التلاتة.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, true, true)]
    [InlineData(UserRole.Manager, true, false)]
    [InlineData(UserRole.FloorManager, true, false)]
    [InlineData(UserRole.Accountant, false, false)]
    [InlineData(UserRole.Technician, false, false)]
    public async Task Who_passes_each_gate(UserRole role, bool deletes, bool restores)
    {
        Assert.Equal(deletes, await Allowed(role, nameof(ReportsController.Delete)));
        Assert.Equal(deletes, await Allowed(role, nameof(ReportsController.Edits)));
        Assert.Equal(restores, await Allowed(role, nameof(ReportsController.Restore)));
    }
}
