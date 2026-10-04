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
/// عناوين محطات الفحص وسياساتها.
///
/// <para>🔴 <b>كلها المالك بس — والسبب ليه.</b> اللي بيقدر يعمل كود
/// تفعيل بيقدر يضيف محطة تقرا وترفع في الورشة، واللي بيقدر يلغي
/// بيقدر يوقّف خط الفحص كله.</para>
///
/// <para>⚠️ <b>والسياسة على الكلاس هنا، مش على كل إجراء.</b> فالفحص
/// ده بيقيس حاجتين: إن الكلاس عليه <c>OwnerOnly</c>، وإن
/// <b>مفيش</b> إجراء شايل سياسة بتوسّع الوصول — ASP.NET بيجمع
/// سياسة الكلاس وسياسة الإجراء بـ«و»، فالإجراء مش بيقدر يفتح، بس
/// سياسة على إجراء بتخلّي اللي بيقرا يفتكر إنها هي الحاكمة.</para>
/// </summary>
public class RackRouteGuardTests
{
    private static HttpMethodAttribute Route(string action) =>
        typeof(RacksController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

    public static TheoryData<string, string, string> Expected => new()
    {
        { nameof(RacksController.List), "GET", "" },
        { nameof(RacksController.Codes), "GET", "activation-codes" },
        { nameof(RacksController.CreateCode), "POST", "activation-codes" },
        { nameof(RacksController.DeleteCode), "DELETE", "activation-codes/{id:guid}" },
        { nameof(RacksController.Suspend), "POST", "{id:guid}/suspend" },
        { nameof(RacksController.Activate), "POST", "{id:guid}/activate" },
        { nameof(RacksController.Revoke), "POST", "{id:guid}/revoke" },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Every_action_keeps_its_url(string action, string verb, string template)
    {
        var route = Route(action);

        Assert.Equal([verb], route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    [Fact]
    public void No_rack_action_is_left_out_of_this_matrix()
    {
        int actions = typeof(RacksController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

        Assert.Equal(Expected.Count(), actions);
    }

    [Fact]
    public void The_rack_prefix_is_frozen()
    {
        var route = typeof(RacksController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        Assert.Equal("api/v1/racks", route.Template);
    }

    /// <summary>
    /// 🔴 <b>السياسة على الكلاس، ومفيش إجراء شايل واحدة.</b>
    ///
    /// <para>⚠️ وده عكس كنترولر الصيانة بالظبط: هناك الكلاس من غير
    /// سياسة وكل إجراء بيختار بتاعته، لأن المحاسب بيشوف القراية
    /// ومابيكتبش. هنا الجمهور واحد — فسياسة واحدة في مكان
    /// واحد.</para>
    /// </summary>
    [Fact]
    public void The_whole_controller_is_owner_only()
    {
        var onClass = typeof(RacksController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal(Policies.OwnerOnly, onClass.Policy);

        var onActions = typeof(RacksController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .SelectMany(m => m.GetCustomAttributes().OfType<AuthorizeAttribute>())
            .ToList();

        Assert.Empty(onActions);
    }

    /// <summary>
    /// 🔴 <b>وأعضاء السياسة نفسها.</b> الفحص اللي فوق بيقيس إن
    /// <c>OwnerOnly</c> مكتوبة؛ ده بيقيس إن مفيش دور تاني جوّاها —
    /// سياسة باسم صح وأعضاء غلط بتعدّي عليه.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.FloorManager, false)]
    [InlineData(UserRole.Accountant, false)]
    [InlineData(UserRole.Technician, false)]
    public async Task Only_the_owner_passes_the_rack_policy(UserRole role, bool allowed)
    {
        var authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(o => o.AddCodlekPolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], "test"));

        var result = await authorization.AuthorizeAsync(user, null, Policies.OwnerOnly);

        Assert.Equal(allowed, result.Succeeded);
    }

    /// <summary>
    /// ⚠️ <b>قيد <c>:guid</c> على كل مسار فيه معرّف.</b> من غيره،
    /// <c>DELETE /racks/activation-codes/abc</c> كان بيوصل للإجراء
    /// ويرجّع <c>400</c> من ربط النوع — ومع التوجيه، نص زي
    /// <c>activation-codes</c> كان بيقدر يتطابق مع
    /// <c>{id}/suspend</c> لو الشكل اتغيّر.
    /// </summary>
    [Fact]
    public void Every_id_segment_is_constrained_to_a_guid()
    {
        var withId = Expected
            .Select(row => (string)row[2]!)
            .Where(template => template.Contains('{'))
            .ToList();

        Assert.Equal(4, withId.Count);

        foreach (string template in withId)
            Assert.Contains("{id:guid}", template);
    }
}
