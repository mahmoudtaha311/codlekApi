using Codlek.Application.Abstractions;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;

namespace Codlek.Application.Features.Repairs;

/// <summary>
/// أسباب فشل أوامر الصيانة.
///
/// <para>🔴 <b>الرسايل منقولة من <c>RepairService</c> بالحرف.</b>
/// المشروعين هيشتغلوا على نفس القاعدة فترة التحويل، فنفس الطلب لازم
/// يدّي نفس الرسالة من الشاشتين.</para>
///
/// <para>⚠️ والأكواد جديدة — القديم مكانش بيرجّع أكواد، كان بيرجّع
/// نص بس. والكود هو اللي الواجهة بتتفرّع عليه.</para>
/// </summary>
public static class RepairErrors
{
    // =================================================================
    //  الفتح
    // =================================================================

    public static readonly Error DeviceNotFound =
        new("repair.device_not_found", "الجهاز مش موجود.", 404);

    /// <summary>
    /// 🔴 الجهاز المدموج مش لاب مستقل — أمره بيتفتح على الكانوني.
    /// </summary>
    public static readonly Error DeviceMerged =
        new("repair.device_merged",
            "الجهاز ده مدموج في جهاز تاني — افتح الأمر على الكانوني.", 400);

    public static readonly Error SourceReportNotFound =
        new("repair.source_report_not_found", "الفحص المصدر مش موجود.", 404);

    // =================================================================
    //  الأمر
    // =================================================================

    public static readonly Error NotFound =
        new("repair.not_found", "أمر الصيانة مش موجود.", 404);

    public static readonly Error ClosedCannotAssign =
        new("repair.closed_cannot_assign", "الأمر ده مقفول — مينفعش يتسند.", 400);

    public static readonly Error ClosedCannotEdit =
        new("repair.closed_cannot_edit", "الأمر ده مقفول — مينفعش يتعدّل.", 400);

    /// <summary>
    /// ⚠️ الرسالة بتقول الحالة بالاسم — «مينفعش تبدأ أمر حالته
    /// «ملغاة»» بتفهّم المستخدم، و«مينفعش» لوحدها لأ.
    /// </summary>
    public static Error CannotStartFrom(RepairStatus status) =>
        new("repair.cannot_start",
            $"مينفعش تبدأ أمر حالته «{RepairStatusRules.Text(status)}».", 400);

    // =================================================================
    //  الفني
    // =================================================================

    public static readonly Error TechnicianNotFound =
        new("repair.technician_not_found", "الفني مش موجود في الشركة دي.", 404);

    public static readonly Error TechnicianSuspended =
        new("repair.technician_suspended", "الفني ده موقوف.", 400);

    public static readonly Error TechnicianCannotRepair =
        new("repair.technician_cannot_repair", "الفني ده مالوش صلاحية صيانة.", 400);

    /// <summary>
    /// ⚠️ والرسالة بتقول الماركة بالاسم — المدير بيعرف يختار فني
    /// تاني بدل ما يفضل يجرّب.
    /// </summary>
    public static Error TechnicianOutsideBrand(string brandName) =>
        new("repair.technician_outside_brand",
            $"الفني ده مش مخصّص لماركة «{brandName}».", 400);

    // =================================================================
    //  الموافقة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الباب اللي كان أوسع حاجة في الميزة.</b>
    ///
    /// <para>من غير الحاجز ده، المالك كان بيقدر يبدأ أمر معلّق ويعدّي
    /// على المحاسب <b>في صمت</b>: السجل بيكتب «تجاوز إداري لبدء
    /// الصيانة»، ومابيقولش إن الموافقة مااتخدتش أصلاً.</para>
    ///
    /// <para>⚠️ <b>ومفيش صلاحية بتتخسر.</b> المالك داخل في سياسة
    /// <c>RepairApprover</c>، فهو بيوافق الأول وبعدين يتجاوز —
    /// قرارين مكتوبين بدل واحد بيخبّي التاني.</para>
    /// </summary>
    public static readonly Error NotApprovedYet =
        new("repair.not_approved",
            "الأمر ده لسه مستني موافقة المحاسب — وافق عليه الأول.", 400);

    public static readonly Error AlreadyRejected =
        new("repair.rejected", "الأمر ده اترفض — مينفعش يبدأ.", 400);

    /// <summary>
    /// 🔴 <b>محاسبين على شاشتين بيدوسوا في نفس اللحظة.</b>
    ///
    /// <para>من غير إعادة التأكد من «لسه معلّق» جوّه نفس المعاملة،
    /// التاني بيدهس قرار الأول ويكتب اسمه مكانه.</para>
    /// </summary>
    public static Error AlreadyDecided(RepairApproval approval) =>
        new("repair.already_decided",
            $"الأمر ده اتقرر فيه خلاص — {RepairStatusRules.ApprovalText(approval)}.", 400);

    /// <summary>
    /// 🔴 <b>سبب الرفض إجباري.</b>
    ///
    /// <para>الفني هيشوف السبب، ورفض من غير سبب بيخلّيه يبعت نفس
    /// الطلب تاني.</para>
    /// </summary>
    public static readonly Error RejectionReasonRequired =
        new("repair.rejection_reason_required",
            "اكتب سبب الرفض — الفني هيشوفه.", 400);

    // =================================================================
    //  القفل
    // =================================================================

    public static readonly Error WorkDescriptionRequired =
        new("repair.work_description_required", "اكتب اللي اتعمل في الصيانة.", 400);

    public static readonly Error UnableReasonRequired =
        new("repair.unable_reason_required", "اكتب سبب تعذّر الإصلاح.", 400);

    public static readonly Error CancelReasonRequired =
        new("repair.cancel_reason_required", "اكتب سبب الإلغاء.", 400);
}
