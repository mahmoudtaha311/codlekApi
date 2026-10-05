using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// عناوين صفحة اللاب وسياساتها.
///
/// <para>🔴 <b>الكنترولر كله <c>ManagerOrAbove</c> على الكلاس — ومفيش
/// إجراء شايل سياسة لوحده.</b> زي القديم: فولدر <c>/Devices</c> كله
/// ورا نفس السياسة (<c>Program.cs:217</c>)، وفورم الملاحظة والليبل
/// جوّه نفس الصفحة. إجراء جديد بسياسة أوسع ماكانش هيفرق — السياستين
/// بيتجمعوا بـ«و» — بس أي حد يشيل سياسة الكلاس «عشان يفتح» إجراء،
/// بيفتح الكل.</para>
///
/// <para>⚠️ <b>والفحص ده بالانعكاس مش بسيرفر:</b> المعالج بيشتغل صح
/// والمسار بيرد <c>200</c> لحد مالوش حق — مفيش فحص منطق بيبان منه ده.</para>
/// </summary>
public class DevicesRouteGuardTests
{
    private static MethodInfo Action(string name) =>
        typeof(DevicesController).GetMethod(name)!;

    private static HttpMethodAttribute Route(string action) =>
        Action(action).GetCustomAttributes().OfType<HttpMethodAttribute>().Single();

    /// <summary>
    /// 🔴 المسارات دي بالحرف — اللوحة بتنديها كده.
    /// </summary>
    [Theory]
    [InlineData(nameof(DevicesController.List), "GET", "")]
    [InlineData(nameof(DevicesController.Lookup), "GET", "lookup")]
    [InlineData(nameof(DevicesController.Detail), "GET", "{id:guid}")]
    [InlineData(nameof(DevicesController.Identifiers), "GET", "{id:guid}/identifiers")]
    [InlineData(nameof(DevicesController.Notes), "GET", "{id:guid}/notes")]
    [InlineData(nameof(DevicesController.AddNote), "POST", "{id:guid}/notes")]
    [InlineData(nameof(DevicesController.Label), "GET", "{id:guid}/qr.svg")]
    [InlineData(nameof(DevicesController.Tests), "GET", "{id:guid}/tests")]
    [InlineData(nameof(DevicesController.Timeline), "GET", "{id:guid}/timeline")]
    public void The_urls_are_frozen(string action, string verb, string template)
    {
        var route = Route(action);

        Assert.Equal([verb], route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    [Fact]
    public void The_whole_controller_is_manager_or_above()
    {
        var onClass = typeof(DevicesController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal(Policies.ManagerOrAbove, onClass.Policy);

        Assert.Equal("api/v1/devices", typeof(DevicesController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single().Template);
    }

    /// <summary>
    /// 🔴 <b>ومفيش إجراء بيفتح نفسه.</b> <c>[AllowAnonymous]</c> على
    /// الليبل «عشان <c>&lt;img src&gt;</c> يشتغل» كان هيدّي أي حد
    /// بمعرّف لاب كوده — والحل الصح إن اللوحة تجيبه بالتوكن.
    /// </summary>
    [Fact]
    public void No_device_action_loosens_or_overrides_the_class_guard()
    {
        foreach (var action in Actions())
        {
            var attributes = action.GetCustomAttributes().ToList();

            Assert.Empty(attributes.OfType<AllowAnonymousAttribute>());
            Assert.Empty(attributes.OfType<AuthorizeAttribute>());
        }
    }

    [Fact]
    public void No_device_action_is_left_out_of_this_matrix()
    {
        Assert.Equal(
            new[]
            {
                nameof(DevicesController.AddNote),
                nameof(DevicesController.Detail),
                nameof(DevicesController.Identifiers),
                nameof(DevicesController.Label),
                nameof(DevicesController.List),
                nameof(DevicesController.Lookup),
                nameof(DevicesController.Notes),
                nameof(DevicesController.Tests),
                nameof(DevicesController.Timeline),
            },
            Actions().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static IEnumerable<MethodInfo> Actions() =>
        typeof(DevicesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());
}
