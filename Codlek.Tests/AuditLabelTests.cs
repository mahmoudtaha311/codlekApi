using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Codlek.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// ترجمة سجل المراجعة، وحارس السجل.
///
/// <para>🔴 <b>الفحص الأول هنا اتكتب في المشروع القديم بعد ما كود
/// واحد فضل <u>شهور</u> بيتعرض بالإنجليزي الخام في سجل عربي</b> —
/// ومكانش باين لأنه بيتكتب من خدمة مالهاش نقطة نهاية. فالفحص
/// بالانعكاس هو الحاجة الوحيدة اللي بتلاقي الكود الجديد
/// تلقائياً.</para>
/// </summary>
public class AuditLabelTests
{
    /// <summary>
    /// 🔴 <b>كل كود إجراء معرّف لازم يكون له ترجمة.</b>
    ///
    /// <para>والفحص ده بيعدّي على الثوابت بالانعكاس، فكود جديد بينضاف
    /// بكرة بيوقّع الفحص ده لوحده — مش محتاج حد يفتكر.</para>
    /// </summary>
    [Fact]
    public void Every_defined_action_has_an_arabic_label()
    {
        var missing = AuditLabels.DefinedActions()
            .Where(action => AuditLabels.Action(action) == action)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void There_is_at_least_one_action_so_the_reflection_actually_found_them()
    {
        /*
          ⚠️ **الحاجز ده موجود عشان الفحص اللي فوق مايبقاش فاضي.**

          لو الانعكاس رجّع قايمة فاضية (اتغيّر الكلاس، أو الثوابت
          بقت خصائص)، «مفيش كود مالوش ترجمة» بتبقى صح — والفحص
          بيعدّي وهو مش بيقيس حاجة.
        */
        Assert.True(AuditLabels.DefinedActions().Count >= 20);
    }

    /// <summary>
    /// 🔴 <b>الكود المجهول بيرجع بنفسه، مش بـ«غير معروف».</b>
    ///
    /// <para>سطر سجل مالوش ترجمة لازم يفضل <b>مقروء</b> — إخفاؤه
    /// بيخلّي الرقابة ناقصة من غير ما حد يعرف.</para>
    /// </summary>
    [Fact]
    public void An_unknown_action_is_shown_as_it_is()
    {
        Assert.Equal("future.thing", AuditLabels.Action("future.thing"));

        // ⚠️ والفاضي بياخد شرطة — مش نص فاضي يخلّي الخانة تبان مكسورة.
        Assert.Equal("—", AuditLabels.Action(null));
        Assert.Equal("—", AuditLabels.Action("  "));
    }

    /// <summary>
    /// ⚠️ <b>نوع الكيان بيتقارن بحروف صغيرة.</b> سجل الإنتاج فيه
    /// <c>"repair"</c> و<c>"Department"</c> و<c>"Device"</c> —
    /// الاختلاف موجود في البيانات، فالترجمة لازم تعدّي عليه.
    /// </summary>
    [Theory]
    [InlineData("repair", "أمر صيانة")]
    [InlineData("Repair", "أمر صيانة")]
    [InlineData("Device", "جهاز")]
    [InlineData("device", "جهاز")]
    [InlineData("rack", "محطة فحص")]
    [InlineData("", "—")]
    public void Entity_types_are_translated_regardless_of_case(string type, string label) =>
        Assert.Equal(label, AuditLabels.Entity(type));

    /// <summary>⚠️ ونوع كيان مجهول بيرجع بنفسه كمان.</summary>
    [Fact]
    public void An_unknown_entity_type_is_shown_as_it_is() =>
        Assert.Equal("Widget", AuditLabels.Entity("Widget"));

    [Theory]
    [InlineData("user", "مستخدم")]
    [InlineData("System", "النظام")]
    [InlineData("rack", "محطة فحص")]
    [InlineData("whatever", "—")]
    public void Actor_types_are_translated(string type, string label) =>
        Assert.Equal(label, AuditLabels.Actor(type));

    // =================================================================
    //  أكواد المشروع القديم
    // =================================================================

    /*
      🔴 **المشروع الجديد بيقرا نفس قاعدة بيانات القديم.**

      يعني صفحة السجل في الجديد بتعرض صفوف كتبها القديم — بأكواد
      الجديد ممكن مايعرفش عنها حاجة. والفحص بالانعكاس اللي فوق
      **مابيشوفش** دي: هو بيعدّي على ثوابت الجديد، فكود موجود في
      البيانات وبس بيعدّي من تحته.

      ⚠️ **وده مش فرض نظري.** تمن أكواد من دول (`rack.*` الستة
      و`device.identity_merged`) ماكانش ليها ترجمة في الجديد لحد ما
      اتضافت مع قطاع المحطات — يعني السجل كان هيعرض إنجليزي خام
      جوّه صفحة عربية.

      ⚠️ **والقايمة دي مستخرجة من**
      `CodlekWeb/Services/AuditActions.cs`:

          grep -oP '(?<== ")[a-z_]+\.[a-z_]+(?=")' AuditActions.cs | sort

      ولما القديم يتشال، القايمة دي بتبقى هي السجل الوحيد لللي
      موجود في البيانات التاريخية — فمتتشالش معاه.
    */
    public static readonly string[] WrittenByTheOldProject =
    [
        "brand.alias_added",
        "brand.alias_removed",
        "brand.created",
        "brand.updated",
        "department.created",
        "department.updated",
        "device.handed_over",
        "device.identity_merged",
        "device.marked_ready",
        "device.sent_to_repair",
        "rack.clone_suspected",
        "rack.code_created",
        "rack.code_deleted",
        "rack.paired",
        "rack.resumed",
        "rack.revoked",
        "rack.suspended",
        "repair.approved",
        "repair.assigned",
        "repair.cancelled",
        "repair.completed",
        "repair.created",
        "repair.reassigned",
        "repair.rejected",
        "repair.retest_started",
        "repair.started",
        "repair.unable_to_repair",
        "technician.activated",
        "technician.brands_changed",
        "technician.capability_changed",
        "technician.created",
        "technician.password_reset",
        "technician.suspended",
        "technician.updated",
        "webuser.activated",
        "webuser.created",
        "webuser.password_reset",
        "webuser.suspended",
    ];

    [Fact]
    public void Every_action_the_old_project_writes_is_translated()
    {
        var missing = WrittenByTheOldProject
            .Where(action => AuditLabels.Action(action) == action)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// ⚠️ حاجز على القايمة نفسها — واحدة اتقصّت بالغلط بتخلّي
    /// الفحص اللي فوق يعدّي وهو بيقيس أقل.
    /// </summary>
    [Fact]
    public void The_old_vocabulary_list_is_complete()
    {
        Assert.Equal(38, WrittenByTheOldProject.Length);
        Assert.Equal(
            WrittenByTheOldProject.Length, WrittenByTheOldProject.Distinct().Count());
    }

    // =================================================================
    //  حارس السجل
    // =================================================================

    /// <summary>
    /// 🔴 <b>للمالك وبس — ودي مقصودة.</b>
    ///
    /// <para>مدير المخزن هو اللي بيمسح ويعدّل، فلو هو اللي بيراجع
    /// تبقى الرقابة بلا معنى.</para>
    /// </summary>
    [Fact]
    public void The_audit_log_is_owner_only()
    {
        var authorize = typeof(AuditController)
            .GetCustomAttributes()
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal(Policies.OwnerOnly, authorize.Policy);

        var route = typeof(AuditController)
            .GetCustomAttributes()
            .OfType<RouteAttribute>()
            .Single();

        Assert.Equal("api/v1/audit", route.Template);
    }

    /// <summary>
    /// 🔴 <b>قراية وبس — ولا مسار كتابة واحد.</b>
    ///
    /// <para>سجل المراجعة اللي بيتعدّل مش سجل مراجعة. والفحص ده
    /// بيلقط أي <c>POST</c> أو <c>PUT</c> أو <c>DELETE</c> بينضاف
    /// بعدين.</para>
    /// </summary>
    [Fact]
    public void The_audit_controller_has_no_write_route()
    {
        var verbs = typeof(AuditController)
            .GetMethods()
            .SelectMany(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .ToList();

        Assert.Equal(["GET"], verbs);
    }
}
