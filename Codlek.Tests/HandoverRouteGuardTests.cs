using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// عناوين التسليم وسياساتها.
///
/// <para>🔴 <b>نقطة واحدة بس هي اللي ليها سياسة مختلفة، ودي
/// الميزة.</b> صاحب الشغل قال عن مدير الدور بالنص: «عنده نفس كل
/// حاجة عند المدير بس عنده ميزة زيادة إنه يقدر يسلّم لابات». فلو
/// تنفيذ التسليم اتحطّ على <c>ManagerOrAbove</c> يبقى
/// <b>مش ميزة زيادة</b> — والفحص ده بيمنع ده.</para>
/// </summary>
public class HandoverRouteGuardTests
{
    private static HttpMethodAttribute Route(string action) =>
        typeof(HandoverController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

    private static string? Policy(string action) =>
        typeof(HandoverController)
            .GetMethod(action)!
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single().Policy;

    public static TheoryData<string, string, string, string> Expected => new()
    {
        {
            nameof(HandoverController.Destinations), "GET", "destinations",
            Policies.ManagerOrAbove
        },
        {
            nameof(HandoverController.Candidates), "GET", "candidates",
            Policies.ManagerOrAbove
        },
        {
            nameof(HandoverController.CandidateIds), "GET", "candidate-ids",
            Policies.ManagerOrAbove
        },
        { nameof(HandoverController.Ready), "POST", "ready", Policies.ManagerOrAbove },
        { nameof(HandoverController.Recipients), "GET", "recipients", Policies.ManagerOrAbove },

        // 🔴 دي الوحيدة.
        { nameof(HandoverController.Execute), "POST", "", Policies.HandoverAllowed },

        { nameof(HandoverController.Log), "GET", "log", Policies.ManagerOrAbove },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Every_action_keeps_its_url_and_its_policy(
        string action, string verb, string template, string policy)
    {
        var route = Route(action);

        Assert.Equal([verb], route.HttpMethods);
        Assert.Equal(template, route.Template);
        Assert.Equal(policy, Policy(action));
    }

    /// <summary>
    /// 🔴 <b>الميزة بتتقاس هنا:</b> التسليم <b>وبس</b> هو اللي على
    /// <c>HandoverAllowed</c>، وكل الباقي على
    /// <c>ManagerOrAbove</c>.
    /// </summary>
    [Fact]
    public void Executing_the_handover_is_the_only_floor_manager_privilege()
    {
        var byPolicy = typeof(HandoverController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .GroupBy(m => m.GetCustomAttributes()
                .OfType<AuthorizeAttribute>()
                .Single().Policy)
            .ToDictionary(g => g.Key!, g => g.Select(m => m.Name).ToList());

        Assert.Equal(
            [nameof(HandoverController.Execute)],
            byPolicy[Policies.HandoverAllowed]);

        Assert.Equal(6, byPolicy[Policies.ManagerOrAbove].Count);

        // ⚠️ ومفيش سياسة تالتة — أي إجراء جديد لازم يختار واحدة من
        // الاتنين بوعي.
        Assert.Equal(2, byPolicy.Count);
    }

    /// <summary>
    /// 🔴 <b>وحارس الكلاس من غير سياسة.</b> ASP.NET بيجمع سياسة
    /// الكلاس وسياسة الإجراء بـ«و» — فـ<c>HandoverAllowed</c> على
    /// الكلاس كانت هتمنع المدير من قراية الجهات والسجل.
    /// </summary>
    [Fact]
    public void The_class_level_gate_carries_no_policy()
    {
        var onClass = typeof(HandoverController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Null(onClass.Policy);
        Assert.Null(onClass.Roles);
    }

    [Fact]
    public void The_handover_prefix_is_frozen()
    {
        var route = typeof(HandoverController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        Assert.Equal("api/v1/handover", route.Template);
    }

    [Fact]
    public void No_handover_action_is_left_out_of_this_matrix()
    {
        int actions = typeof(HandoverController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

        Assert.Equal(Expected.Count(), actions);
    }

    /// <summary>
    /// 🔴 <b>ومدير الدور لازم يكون جوّه السياسة دي فعلاً.</b> الفحص
    /// اللي فوق بيقيس إن <c>HandoverAllowed</c> مكتوبة على النقطة؛
    /// ده بيقيس إن <b>أعضاءها</b> صح — لأن سياسة باسم صح وأعضاء غلط
    /// بتعدّي عليه.
    /// </summary>
    [Theory]
    [InlineData(Core.Enums.UserRole.FloorManager, true)]
    [InlineData(Core.Enums.UserRole.Owner, true)]
    [InlineData(Core.Enums.UserRole.Manager, false)]
    [InlineData(Core.Enums.UserRole.Accountant, false)]
    [InlineData(Core.Enums.UserRole.Technician, false)]
    public async Task The_handover_policy_membership_is_frozen(
        Core.Enums.UserRole role, bool allowed)
    {
        var authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(o => o.AddCodlekPolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        var user = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Role, role.ToString())],
                "test"));

        var result = await authorization.AuthorizeAsync(user, null, Policies.HandoverAllowed);

        Assert.Equal(allowed, result.Succeeded);
    }
}
