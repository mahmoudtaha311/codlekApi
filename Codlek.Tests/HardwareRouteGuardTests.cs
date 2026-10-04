using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// عناوين العتاد وسياساتها.
///
/// <para>🔴 <b>السياسة اللي اتصلّحت هنا كانت <u>ناقصة</u> في
/// المشروع القديم.</b> مسار المقارنة كان متسجّل على المجموعة
/// الجذر، وسياسات المجموعات بتتطبّق على المجموعة نفسها مش على شكل
/// المسار — فـ<c>/devices/{id}/compare</c> ماكانش وراه أي حارس غير
/// «مسجّل دخول». يعني أي فني كان بيقدر يقارن لقطات أي جهاز في
/// الشركة ويقرا سيريالات بضاعة مش شغله.</para>
///
/// <para>⚠️ <b>والفحص ده لازم يفضل بالانعكاس مش بسيرفر.</b> الغلطة
/// دي مابتظهرش في أي فحص منطق: المعالج بيشتغل صح، والمسار بيرد
/// <c>200</c> لحد مالوش حق.</para>
/// </summary>
public class HardwareRouteGuardTests
{
    private static HttpMethodAttribute Route(string action) =>
        typeof(HardwareController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

    private static string? Policy(string action) =>
        typeof(HardwareController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault()?.Policy;

    /// <summary>
    /// 🔴 المسارات دي بالحرف — الداش بورد بتنديها كده، وحرف واحد
    /// مختلف معناه <c>404</c> على تبويب شغّال.
    /// </summary>
    [Theory]
    [InlineData(nameof(HardwareController.ForReport), "api/v1/reports/{id:guid}/hardware")]
    [InlineData(nameof(HardwareController.ForDevice), "api/v1/devices/{deviceId:guid}/hardware")]
    [InlineData(nameof(HardwareController.Compare), "api/v1/devices/{deviceId:guid}/compare")]
    public void The_urls_are_frozen(string action, string template)
    {
        var route = Route(action);

        Assert.Equal(["GET"], route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    /// <summary>
    /// 🔴 <b>المقارنة ولقطة الجهاز للمديرين وفوق — والفني
    /// برّه.</b>
    /// </summary>
    [Theory]
    [InlineData(nameof(HardwareController.ForDevice))]
    [InlineData(nameof(HardwareController.Compare))]
    public void The_device_level_endpoints_require_a_manager(string action) =>
        Assert.Equal(Policies.ManagerOrAbove, Policy(action));

    /// <summary>
    /// ⚠️ <b>ولقطة الفحص مالهاش سياسة بالاسم عن قصد.</b>
    ///
    /// <para>الفني بيوصلها عشان يشوف <b>شغله هو</b>، والحارس جوّه
    /// المعالج لأنه محتاج يقرا الصف الأول عشان يعرف الفحص بتاع مين.
    /// والسياسة هنا كانت هتمنعه خالص.</para>
    /// </summary>
    [Fact]
    public void The_report_snapshot_is_guarded_inside_the_handler_not_by_a_policy()
    {
        Assert.Null(Policy(nameof(HardwareController.ForReport)));

        // ⚠️ وحارس الكلاس «مسجّل دخول» وبس — أي سياسة هنا بتتجمع
        // بـ«و» وبتمنع الفني.
        var onClass = typeof(HardwareController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Null(onClass.Policy);
        Assert.Null(onClass.Roles);
    }

    /// <summary>
    /// ⚠️ ومفيش <c>[Route]</c> على الكلاس: المسارات التلاتة على
    /// بادئتين مختلفتين (<c>reports</c> و<c>devices</c>)، فكل إجراء
    /// بيكتب مساره كامل.
    /// </summary>
    [Fact]
    public void The_class_carries_no_route_prefix()
    {
        Assert.Empty(typeof(HardwareController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>());
    }

    [Fact]
    public void No_hardware_action_is_left_out_of_this_matrix()
    {
        var actions = typeof(HardwareController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(
            [nameof(HardwareController.ForReport),
             nameof(HardwareController.ForDevice),
             nameof(HardwareController.Compare)],
            actions.OrderBy(n => n, StringComparer.Ordinal)
                   .OrderBy(n => n switch
                   {
                       nameof(HardwareController.ForReport) => 0,
                       nameof(HardwareController.ForDevice) => 1,
                       _ => 2,
                   }));
    }
}
