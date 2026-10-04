using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Workflow;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces;

/// <summary>
/// انتقالات أمر الصيانة — <b>مكان واحد، والمعالجات بتناديه</b>.
///
/// <para>🔴 <b>ليه خدمة مشتركة بدل ما كل معالج يكتب انتقاله.</b>
/// الوثيقة في المشروع القديم صريحة في النقطة دي: الانتقالات دي
/// <b>مش</b> بتتنده من نقط HTTP بس — <b>مسار المزامنة من الراكة
/// بيوصل لنفسها</b>. فلو كل معالج كتب انتقاله جوّاه، ميزة المزامنة
/// لما تنزل هتضطر تكرّر السبعة — <b>وفني أوفلاين يقدر يقفل أمر مش
/// من حقه يقفله</b>.</para>
///
/// <para>⚠️ <b>ومفيش <c>SaveChanges</c> في ولا دالة هنا.</b> الحفظ
/// مسؤولية المنادي، عشان السجل والانتقال والحركة ينزلوا في حفظة
/// واحدة.</para>
///
/// <para>⚠️ <b>والدوال بترجّع <c>Result</c> مش بترمي.</b> «الأمر
/// مقفول» نتيجة متوقّعة ليها معنى عند المستخدم.</para>
/// </summary>
public interface IRepairTransitions
{
    /// <summary>
    /// بيتأكّد من الفني — <b>والماركة على حسب النيّة</b>.
    /// </summary>
    /// <param name="brandCheck">
    /// 🔴 <b>مفيش قيمة افتراضية.</b> كل مكان لازم يقول نيّته بالاسم.
    /// </param>
    Task<Result<Technician>> ResolveTechnicianAsync(
        Guid tenantId,
        Guid technicianId,
        Guid? deviceId,
        Core.Repairs.RepairPolicy.BrandCheck brandCheck,
        CancellationToken ct = default);

    /// <summary>
    /// إسناد الأمر لفني.
    ///
    /// <para>⚠️ <b>مفيش حاجز موافقة هنا.</b> أمر معلّق <b>ينفع</b>
    /// يتسند — اللي محتاج موافقة هو <b>البدء</b>.</para>
    /// </summary>
    /// <param name="allowOutsideBrand">
    /// 🔴 المحاسب بيعدّي القيد. <b>والتعدية بتتسجّل على الأمر نفسه</b>
    /// (<c>BrandOverride</c>) — مش في ملاحظة حرة حد يقدر يمسحها.
    /// </param>
    Task<Result<RepairWorkItem>> AssignAsync(
        Guid tenantId,
        Guid workItemId,
        Guid technicianId,
        WorkflowActor actor,
        bool allowOutsideBrand,
        CancellationToken ct = default);

    /// <summary>
    /// بدء الشغل.
    ///
    /// <para>🔴 <b>وده اللي فيه حاجز الموافقة</b> — أوسع باب في
    /// الميزة كلها.</para>
    /// </summary>
    /// <param name="startedAtUtc">
    /// ⚠️ <c>null</c> = دلوقتي. والراكة بتبعت وقتها الحقيقي عشان
    /// الشغل الأوفلاين يتسجّل بوقته.
    /// </param>
    Task<Result<RepairWorkItem>> StartAsync(
        Guid tenantId,
        Guid workItemId,
        Guid technicianId,
        WorkflowActor actor,
        DateTime? startedAtUtc = null,
        CancellationToken ct = default);

    /// <summary>إلغاء الأمر — <b>ومفيش حركة جهاز</b>.</summary>
    Task<Result<RepairWorkItem>> CancelAsync(
        Guid tenantId,
        Guid workItemId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// تعديل بيانات الشغل.
    ///
    /// <para>🔴 <b>مفيش نقطة HTTP على الدالة دي — ولا على القفل ولا
    /// على التعذّر.</b> القديم فيه <c>UpdateRepairRequest</c> مكتوب في
    /// العقود و<b>مفيش ولا مسار بيربطه</b>؛ الدخول الوحيد هو مسار
    /// المزامنة من الراكة. فتح مسار جديد هنا بيزوّد سطح
    /// <b>مكانش موجود</b> على قاعدة مشتركة بين المشروعين.</para>
    /// </summary>
    /// <param name="diagnosis">
    /// ⚠️ <c>null</c> = «مابعتّوش»، والنص الفاضي <b>بيمسح</b>.
    /// </param>
    Task<Result<RepairWorkItem>> UpdateWorkAsync(
        Guid tenantId,
        Guid workItemId,
        string? diagnosis,
        string? actions,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// قفل الأمر بنجاح.
    ///
    /// <para>⚠️ <b>ومفيش فحص ماركة.</b> اللاب اتصلّح خلاص — الرفض
    /// بيمسح الدليل مش بيرجّع الشغل.</para>
    /// </summary>
    Task<Result<RepairWorkItem>> CompleteAsync(
        Guid tenantId,
        Guid workItemId,
        Guid technicianId,
        string actions,
        string? notes,
        WorkflowActor actor,
        DateTime? completedAtUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// تعذّر الإصلاح.
    ///
    /// <para>🔴 <b>وبيكتب نفس نوع الحركة بتاع القفل الناجح.</b> أي
    /// تقرير بيعدّ النجاح بنوع الحركة بيعدّ الفشل معاه.</para>
    /// </summary>
    Task<Result<RepairWorkItem>> MarkUnableAsync(
        Guid tenantId,
        Guid workItemId,
        Guid technicianId,
        string reason,
        WorkflowActor actor,
        DateTime? atUtc = null,
        CancellationToken ct = default);
}
