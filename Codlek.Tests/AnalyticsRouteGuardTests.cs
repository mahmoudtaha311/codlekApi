using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// عناوين التحليلات وسياساتها.
///
/// <para>🔴 <b>تلات مستويات صلاحية هنا، وكل واحد بيعني حاجة
/// مختلفة:</b> بلا سياسة (الفني بيشوفها مضيّقة) · بلا سياسة وبترجّع
/// أصفار (التحذيرات، لأنها بتتنده على كل صفحة) ·
/// <c>ManagerOrAbove</c> (أرقام على مستوى الشركة).</para>
///
/// <para>⚠️ <b>والفرق ده مابيظهرش في أي فحص منطق:</b> حطّ سياسة
/// على اللوحة والفني بياخد <c>403</c> على صفحته هو؛ وشيل السياسة
/// عن الجرد والفني بيقرا جرد الورشة كله.</para>
/// </summary>
public class AnalyticsRouteGuardTests
{
    private static HttpMethodAttribute Route(string action) =>
        typeof(AnalyticsController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

    private static string? Policy(string action) =>
        typeof(AnalyticsController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault()?.Policy;

    /// <summary>
    /// 🔴 المسارات دي بالحرف — الداش بورد بتنديها كده.
    /// </summary>
    [Theory]
    [InlineData(nameof(AnalyticsController.Dashboard), "api/v1/dashboard")]
    [InlineData(nameof(AnalyticsController.Breakdown), "api/v1/dashboard/breakdown")]
    [InlineData(nameof(AnalyticsController.Alerts), "api/v1/alerts/summary")]
    [InlineData(nameof(AnalyticsController.Inventory), "api/v1/inventory/summary")]
    [InlineData(nameof(AnalyticsController.PartsDemand), "api/v1/parts-demand")]
    [InlineData(nameof(AnalyticsController.DailyReport), "api/v1/daily-report")]
    public void The_urls_are_frozen(string action, string template)
    {
        var route = Route(action);

        Assert.Equal(["GET"], route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    /// <summary>
    /// 🔴 <b>الأرقام على مستوى الشركة بسياسة، والمضيّقة من
    /// غيرها.</b>
    /// </summary>
    [Theory]
    [InlineData(nameof(AnalyticsController.Inventory), Policies.ManagerOrAbove)]
    [InlineData(nameof(AnalyticsController.PartsDemand), Policies.ManagerOrAbove)]
    [InlineData(nameof(AnalyticsController.Dashboard), null)]
    [InlineData(nameof(AnalyticsController.Breakdown), null)]
    [InlineData(nameof(AnalyticsController.DailyReport), null)]
    [InlineData(nameof(AnalyticsController.Alerts), null)]
    public void The_policy_matrix_is_frozen(string action, string? policy) =>
        Assert.Equal(policy, Policy(action));

    /// <summary>
    /// ⚠️ <b>وحارس الكلاس «مسجّل دخول» وبس.</b> أي سياسة هنا بتتجمع
    /// بـ«و» مع سياسة الإجراء — فـ<c>ManagerOrAbove</c> على الكلاس
    /// كانت هتمنع الفني من لوحته هو.
    /// </summary>
    [Fact]
    public void The_class_level_gate_carries_no_policy()
    {
        var onClass = typeof(AnalyticsController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Null(onClass.Policy);
        Assert.Null(onClass.Roles);

        // ⚠️ ومفيش بادئة على الكلاس: المسارات على بادئات مختلفة.
        Assert.Empty(typeof(AnalyticsController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>());
    }

    [Fact]
    public void No_analytics_action_is_left_out_of_this_matrix()
    {
        int actions = typeof(AnalyticsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

        Assert.Equal(6, actions);
    }

    /// <summary>
    /// 🔴 <b>والتحليلات كلها قراية — ولا مسار كتابة واحد.</b>
    /// </summary>
    [Fact]
    public void Analytics_is_read_only()
    {
        var verbs = typeof(AnalyticsController)
            .GetMethods()
            .SelectMany(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .ToList();

        Assert.Equal(["GET"], verbs);
    }
}
