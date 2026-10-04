using Codlek.Application.Abstractions;
using Codlek.Application.Features.Repairs;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// كتالوج أخطاء الصيانة.
///
/// <para>🔴 <b>الرسايل منقولة من المشروع القديم بالحرف</b> — المشروعين
/// هيشتغلوا على نفس القاعدة فترة التحويل، فنفس الطلب لازم يدّي نفس
/// الرسالة من الشاشتين.</para>
/// </summary>
public class RepairErrorCatalogTests
{
    private static IEnumerable<Error> All() =>
    [
        RepairErrors.DeviceNotFound,
        RepairErrors.DeviceMerged,
        RepairErrors.SourceReportNotFound,
        RepairErrors.NotFound,
        RepairErrors.ClosedCannotAssign,
        RepairErrors.ClosedCannotEdit,
        RepairErrors.TechnicianNotFound,
        RepairErrors.TechnicianSuspended,
        RepairErrors.TechnicianCannotRepair,
        RepairErrors.NotApprovedYet,
        RepairErrors.AlreadyRejected,
        RepairErrors.RejectionReasonRequired,
        RepairErrors.WorkDescriptionRequired,
        RepairErrors.UnableReasonRequired,
        RepairErrors.CancelReasonRequired,
        RepairErrors.CannotStartFrom(RepairStatus.Cancelled),
        RepairErrors.TechnicianOutsideBrand("HP"),
        RepairErrors.AlreadyDecided(RepairApproval.Approved),
    ];

    /// <summary>
    /// 🔴 <b>كل سبب فشل عنده كود لوحده.</b>
    ///
    /// <para>الواجهة بتتفرّع على الكود مش على الرسالة. ولو اتنين
    /// اشتركوا في كود، الواجهة مش هتعرف تفرّق — مثلاً تودّي على شاشة
    /// الموافقة في حالة «الفني موقوف».</para>
    /// </summary>
    [Fact]
    public void Every_repair_error_has_its_own_code()
    {
        var codes = All().Select(e => e.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    /// <summary>⚠️ وكلها بتبدأ بـ<c>repair.</c> — فالسجل بيتفلتر.</summary>
    [Fact]
    public void Every_code_is_namespaced()
    {
        foreach (var error in All())
            Assert.StartsWith("repair.", error.Code);
    }

    /// <summary>
    /// 🔴 <b>الرسايل العربية دي عقد.</b>
    ///
    /// <para>الفحص ده مكتوب بالإيد عن قصد: لو حد «حسّن» صياغة، الفحص
    /// بيقع ويقول إن اللي بيتعرض للمستخدم اتغيّر.</para>
    /// </summary>
    [Fact]
    public void The_messages_match_the_old_project_verbatim()
    {
        Assert.Equal("الجهاز مش موجود.", RepairErrors.DeviceNotFound.Description);

        Assert.Equal(
            "الجهاز ده مدموج في جهاز تاني — افتح الأمر على الكانوني.",
            RepairErrors.DeviceMerged.Description);

        Assert.Equal("أمر الصيانة مش موجود.", RepairErrors.NotFound.Description);
        Assert.Equal("الأمر ده مقفول — مينفعش يتسند.", RepairErrors.ClosedCannotAssign.Description);
        Assert.Equal("الأمر ده مقفول — مينفعش يتعدّل.", RepairErrors.ClosedCannotEdit.Description);
        Assert.Equal("الفني مش موجود في الشركة دي.", RepairErrors.TechnicianNotFound.Description);
        Assert.Equal("الفني ده موقوف.", RepairErrors.TechnicianSuspended.Description);
        Assert.Equal("الفني ده مالوش صلاحية صيانة.", RepairErrors.TechnicianCannotRepair.Description);
        Assert.Equal("الأمر ده اترفض — مينفعش يبدأ.", RepairErrors.AlreadyRejected.Description);
        Assert.Equal("اكتب اللي اتعمل في الصيانة.", RepairErrors.WorkDescriptionRequired.Description);
        Assert.Equal("اكتب سبب تعذّر الإصلاح.", RepairErrors.UnableReasonRequired.Description);
        Assert.Equal("اكتب سبب الإلغاء.", RepairErrors.CancelReasonRequired.Description);

        Assert.Equal(
            "الأمر ده لسه مستني موافقة المحاسب — وافق عليه الأول.",
            RepairErrors.NotApprovedYet.Description);
    }

    /// <summary>
    /// ⚠️ والرسايل المبنية بتحطّ القيمة جوّه النص.
    ///
    /// <para>«مينفعش» لوحدها مابتفهّمش؛ «مينفعش تبدأ أمر حالته
    /// «ملغاة»» بتفهّم.</para>
    /// </summary>
    [Fact]
    public void The_built_messages_name_the_value()
    {
        Assert.Contains("ملغاة", RepairErrors.CannotStartFrom(RepairStatus.Cancelled).Description);
        Assert.Contains("HP", RepairErrors.TechnicianOutsideBrand("HP").Description);

        Assert.Contains(
            "اتوافق عليه",
            RepairErrors.AlreadyDecided(RepairApproval.Approved).Description);
    }

    /// <summary>
    /// 🔴 <b>«مش موجود» ٤٠٤، والباقي ٤٠٠.</b>
    ///
    /// <para>⚠️ ومفيش ولا واحد ٤٠١ ولا ٤٠٣: الصلاحيات بتتقفل في
    /// السياسات، مش في الخدمة. ولو خطأ هنا رجّع ٤٠١، الواجهة بتطرد
    /// المستخدم على شاشة الدخول وهو داخل فعلاً — غلط في خانة بس.</para>
    /// </summary>
    [Fact]
    public void Only_missing_things_are_404_and_nothing_is_401()
    {
        foreach (var error in All())
        {
            bool isMissing = error.Code.EndsWith("not_found");

            Assert.Equal(isMissing ? 404 : 400, error.StatusCode);
        }
    }
}
