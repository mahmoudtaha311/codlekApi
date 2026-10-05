using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Reports;

public static class ReportErrors
{
    public static readonly Error NotFound =
        new("report.not_found", "الفحص مش موجود.", 404);

    /// <summary>
    /// 🔴 <b>الفني مالوش يشوف فحص حد تاني.</b>
    ///
    /// <para>والترتيب مهم: الصف بيتقرا الأول (عشان نعرف بتاع مين)
    /// وبعدين الحارس — فالمعرّف المش موجود بياخد <c>404</c>،
    /// والموجود بتاع غيره بياخد <c>403</c>. وده منقول زي ما هو:
    /// توحيدهم كان بيخبّي الفرق على اللي بيقرا السجل.</para>
    /// </summary>
    public static readonly Error NotYours =
        new("report.not_yours", "الفحص ده مش بتاعك.", 403);

    // =================================================================
    //  المسح والاسترجاع — النصوص من صفحة الفحص في القديم بالحرف
    // =================================================================

    public static readonly Error DeleteForbidden =
        new("report.delete_forbidden", "المسح من صلاحية مدير المخزن وفوق", 403);

    public static readonly Error DeleteReasonRequired =
        new("report.delete_reason_required",
            "لازم تكتب سبب المسح (٥ حروف على الأقل)", 400);

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيقع.</b> العمود ٤٠٠ حرف، والقديم
    /// ماكانش بيفحص الطول فسبب أطول كان بيوقّع الحفظ بخطأ عام. والقص
    /// في صمت مش حل: السبب ده هو الدليل اللي المالك بيراجعه.
    /// </summary>
    public static readonly Error ReasonTooLong =
        new("report.reason_too_long",
            $"السبب أطول من {ReportDeletionRules.MaxReasonLength} حرف — اختصره", 400);

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيقبله.</b> القديم مابيعرضش فورم المسح
    /// على فحص ممسوح، بس النقطة نفسها كانت بتقبل مسح تاني <b>وتكتب
    /// فوق</b> مين مسح وليه — فمديرين فاتحين نفس الصفحة كان التاني
    /// فيهم بيمحي دليل الأول.
    /// </summary>
    public static readonly Error AlreadyDeleted =
        new("report.already_deleted", "الفحص ده ممسوح أصلاً", 409);

    public static readonly Error RestoreForbidden =
        new("report.restore_forbidden", "الاسترجاع من صلاحية المدير العام", 403);

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيقبله.</b> استرجاع فحص مش ممسوح كان
    /// بيكتب «اترجع بواسطة» على فحص عمره ما اتمسح.
    /// </summary>
    public static readonly Error NotDeleted =
        new("report.not_deleted", "الفحص ده مش ممسوح", 409);

    /// <summary>
    /// ⚠️ القديم كان بيخفي الجدول عن غير المديرين من غير رسالة —
    /// والنص هنا على نفس قالب رسالة المسح.
    /// </summary>
    public static readonly Error EditsForbidden =
        new("report.edits_forbidden", "تعديلات الفحص من صلاحية مدير المخزن وفوق", 403);
}
