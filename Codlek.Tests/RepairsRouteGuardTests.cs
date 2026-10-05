using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// عناوين الصيانة وسياساتها — <b>مجمّدة بالحرف</b>.
///
/// <para>🔴 <b>ليه فحص بالانعكاس بدل سيرفر كامل.</b> الحاجتين اللي
/// بتتكسرا هنا صامتتين: حرف في العنوان بيدّي <c>404</c> على تبويب
/// شغّال، وسياسة على الكلاس بتدّي <c>403</c> للمحاسب على قايمة
/// <b>لازم</b> يقراها. والاتنين مابيظهروش في أي فحص منطق.</para>
///
/// <para>🔴 <b>والفخ الأكبر: ASP.NET بيجمع سياسة الكلاس وسياسة
/// الإجراء بـ«و» مش بـ«أو».</b> فـ<c>[Authorize(ManagerOrAbove)]</c>
/// على الكلاس معناه إن المحاسب <b>مايقدرش</b> يقرا ولا نقطة، حتى
/// لو الإجراء مكتوب عليه <c>RepairsViewer</c>. والفحص اللي تحت
/// بيمنع ده.</para>
/// </summary>
public class RepairsRouteGuardTests
{
    /// <summary>
    /// العنوان والسياسة لكل إجراء — <b>نفس أرقام المشروع
    /// القديم</b>.
    /// </summary>
    public static TheoryData<string, string, string, string> Expected => new()
    {
        { nameof(RepairsController.List), "GET", "", Policies.RepairsViewer },
        { nameof(RepairsController.Pending), "GET", "pending", Policies.RepairsViewer },
        { nameof(RepairsController.Technicians), "GET", "technicians", Policies.RepairsViewer },
        { nameof(RepairsController.Detail), "GET", "{id:guid}", Policies.RepairsViewer },

        { nameof(RepairsController.Open), "POST", "", Policies.ManagerOrAbove },
        // ⚠️ الوحيدة اللي اتغيّرت عن القديم — قرار المالك إن المحاسب يسند.
        { nameof(RepairsController.Assign), "POST", "{id:guid}/assign", Policies.RepairAssigner },
        { nameof(RepairsController.Cancel), "POST", "{id:guid}/cancel", Policies.ManagerOrAbove },

        {
            nameof(RepairsController.OverrideStart), "POST",
            "{id:guid}/override/start", Policies.ManagerOrAbove
        },

        { nameof(RepairsController.Approve), "POST", "{id:guid}/approve", Policies.RepairApprover },
        { nameof(RepairsController.Reject), "POST", "{id:guid}/reject", Policies.RepairApprover },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Every_action_keeps_its_url_and_its_policy(
        string action, string verb, string template, string policy)
    {
        var method = typeof(RepairsController).GetMethod(action)!;

        var route = method.GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

        Assert.Equal([verb], route.HttpMethods);
        Assert.Equal(template, route.Template);

        var authorize = method.GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal(policy, authorize.Policy);
    }

    /// <summary>
    /// 🔴 <b>الفحص اللي بيمنع أخطر غلطة في الملف.</b>
    ///
    /// <para><c>[Authorize]</c> على الكلاس لازم تفضل <b>من غير
    /// سياسة</b>: هي بتقول «لازم تكون مسجّل دخول» وبس، والسياسة
    /// الحقيقية على كل إجراء. وأي سياسة هنا بتتجمع مع سياسة الإجراء
    /// بـ«و» — فالمحاسب بياخد <c>403</c> على القايمة والطابور وقايمة
    /// الفنيين، وشاشة الموافقة بتاعته بتبقى فاضية.</para>
    /// </summary>
    [Fact]
    public void The_class_level_gate_carries_no_policy()
    {
        var authorize = typeof(RepairsController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Policy);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public void The_repairs_prefix_is_frozen()
    {
        var route = typeof(RepairsController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        Assert.Equal("api/v1/repairs", route.Template);
    }

    /// <summary>
    /// 🔴 <b>وكل إجراء لازم يكون عليه سياسة بالاسم.</b>
    ///
    /// <para>إجراء بـ<c>[Authorize]</c> فاضية بياخد سياسة الكلاس
    /// بس — يعني «أي مستخدم مسجّل»، والفني بيقدر يوافق على
    /// أوامره هو. والفحص ده بيلقط إجراء جديد بينضاف ومحدش حط
    /// عليه سياسة.</para>
    /// </summary>
    [Fact]
    public void No_action_is_left_without_a_named_policy()
    {
        var actions = typeof(RepairsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .ToList();

        // ⚠️ العدد مجمّد كمان: إجراء جديد لازم يتزاد للجدول اللي فوق.
        Assert.Equal(Expected.Count(), actions.Count);

        Assert.All(actions, m =>
            Assert.False(string.IsNullOrEmpty(
                m.GetCustomAttributes().OfType<AuthorizeAttribute>().Single().Policy)));
    }

    /// <summary>
    /// 🔴 <b>عدد حقول <c>RepairDetail</c> وترتيبها — عقد.</b>
    ///
    /// <para>الداش بورد بتقرا بالأسماء، بس أي <c>record</c> بـ٣٨
    /// حقل فيه أزواج متجاورة من <b>نفس النوع</b>: زوجين فني،
    /// وأربع نصوص ورا بعض، وتلات <c>bool</c> افتراضيهم
    /// <c>false</c>. قلب أي زوج منهم <b>بيترجم</b> ومابيدّيش ولا
    /// تحذير — وبينسب الصيانة لفني غلط.</para>
    ///
    /// <para>⚠️ والعدد نفسه مجمّد: حقل جديد في النص بيزقّ كل اللي
    /// بعديه.</para>
    /// </summary>
    [Fact]
    public void The_detail_contract_keeps_its_field_count_and_order()
    {
        var fields = typeof(Codlek.Application.Contracts.Repairs.RepairDetail)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(38, fields.Count);

        /*
          ⚠️ **أسماء المُنشئ بحرف كبير، والـJSON بحرف صغير.**

          الانعكاس بيدّي `Id` و`PublicCode`؛ والمُسلسِل هو اللي
          بيصغّرهم. وأول نسخة من الفحص ده دوّرت بالأسماء الصغيرة
          فـ`IndexOf` رجّع `-1`، و`Skip(-1)` **مارماش** — قارن من
          أول القايمة وفشل برسالة مالهاش علاقة. فالدالة تحت
          بتتأكد إن الحقل اتلاقى أصلاً.
        */
        void Adjacent(string first, params string[] rest)
        {
            int at = fields.IndexOf(first);

            Assert.True(at >= 0, first + " مش موجود في العقد خالص.");
            Assert.Equal([first, .. rest], fields.Skip(at).Take(rest.Length + 1));
        }

        // الزوجين الخطر: فني متسند وبعديه اللي قفل، وكل واحد معرّف
        // وبعديه اسم.
        Adjacent(
            "AssignedTechnicianId", "AssignedTechnicianName",
            "CompletedByTechnicianId", "CompletedByTechnicianName");

        // والتلات بوول اللي مابينهم نص واحد بس.
        Adjacent("BrandOverride", "StartedWithoutApproval", "Brand", "TechnicianOutsideBrand");

        // وأربع النصوص ورا بعض.
        Adjacent("FaultSummary", "RepairActions", "Notes", "OutcomeReason");
    }

    // =================================================================
    //  تبويب الصيانة في صفحة الجهاز
    // =================================================================

    /// <summary>
    /// 🔴 <b>العنوان ده على بادئة <u>الأجهزة</u>.</b>
    ///
    /// <para>الداش بورد بتنده <c>/api/v1/devices/{id}/repairs</c>
    /// بالحرف. وحطّه تحت <c>/repairs</c> بيدّي <c>404</c> على تبويب
    /// شغّال.</para>
    ///
    /// <para>⚠️ <b>والصلاحية هنا أضيق من قايمة الصيانة:</b> المحاسب
    /// بيقرا <c>/repairs</c> ومابيقراش ده — التبويب جمبه كل بيانات
    /// اللاب.</para>
    /// </summary>
    [Fact]
    public void The_device_tab_hangs_off_the_devices_prefix_and_excludes_the_accountant()
    {
        var route = typeof(DeviceRepairsController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        var authorize = typeof(DeviceRepairsController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        var action = typeof(DeviceRepairsController)
            .GetMethod(nameof(DeviceRepairsController.List))!
            .GetCustomAttributes()
            .OfType<HttpMethodAttribute>()
            .Single();

        Assert.Equal("api/v1/devices", route.Template);
        Assert.Equal("{id:guid}/repairs", action.Template);
        Assert.Equal(Policies.ManagerOrAbove, authorize.Policy);
    }
}
