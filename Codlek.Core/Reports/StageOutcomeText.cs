using Codlek.Core.Hardware;

namespace Codlek.Core.Reports;

/// <summary>
/// نتيجة مرحلة الفحص بالعربي — <b>ومعاها التلات مراحل اللي مالهاش
/// نتيجة فحص أصلاً</b>.
///
/// <para>🔴 <b>تلات مراحل مش فحوص:</b> المواصفات (بتتقرا تلقائي)،
/// والملاحظات (بني آدم بيكتب)، والتسليم (إجراء). الحالة بتوصلهم
/// <c>٠</c>، و<c>StepOutcome.Arabic(0)</c> بيقول «لم يُنفّذ» —
/// فكانوا بيطلعوا «لم يُنفّذ» على <b>كل فحص متسلّم</b> في النظام،
/// والمدير بيقرا إن الفحص ناقص وهو كامل.</para>
///
/// <para>⚠️ <b>والشروط هنا مبنية على أعمدة موجودة أصلاً:</b> وجود
/// الفحص نفسه دليل إن المواصفات اتقرت، و<c>EndedAtUtc</c> بيتكتب
/// عند التسليم وبس. فمفيش عمود جديد ولا هجرة.</para>
/// </summary>
public static class StageOutcomeText
{
    public const string SpecsStep = "specs";
    public const string NotesStep = "notes";
    public const string ReviewStep = "review";

    /// <summary>
    /// ⚠️ النص اللي الراكة بتكتبه في خانة خدمة البطارية لما تكون
    /// محتاجة صيانة.
    /// </summary>
    public const string BatteryNeedsService = "محتاجة صيانة";

    /// <summary>
    /// نص النتيجة.
    /// </summary>
    /// <param name="facts">
    /// ⚠️ الحقايق اللي الحكم محتاجها — مش كيان الفحص كامل: الدالة
    /// نقية وبتتجرّب لوحدها.
    /// </param>
    public static string Arabic(string stepId, int status, StageFacts facts)
    {
        // ⚠️ أي حالة حقيقية بتعدّي على الترجمة العادية.
        if (status != 0) return StepOutcome.Arabic(status);

        return stepId switch
        {
            // المواصفات بتتقرا تلقائي والقراءة نفسها مرفوعة معاها،
            // فوجود الفحص هنا هو الدليل إنها اتقرت.
            SpecsStep => "اتقرت",

            NotesStep => facts.RepairRecorded
                ? "فيه صيانة متسجّلة"
                : facts.HasGeneralNote ? "اتسجّلت" : "مفيش ملاحظات",

            // ⚠️ والشرط مش شكلي: الفحص ممكن يوصل من استيراد شيت من
            // غير تسليم.
            ReviewStep => facts.Handed ? "تم التسليم" : StepOutcome.Arabic(status),

            _ => StepOutcome.Arabic(status),
        };
    }

    /// <summary>
    /// نبرة العرض.
    ///
    /// <para>⚠️ <b>ورقم من نسخة أحدث بياخد «محايد» مش «وحش»</b> —
    /// نفس قاعدة النص: المجهول بيفضل ظاهر، مابيتحكمش عليه.</para>
    /// </summary>
    public static string Tone(string stepId, int status, StageFacts facts) => status switch
    {
        1 => StepOutcomeTone.Good,
        2 => StepOutcomeTone.Bad,
        3 => StepOutcomeTone.Neutral,

        // ⚠️ «مش موجود» و«تعذّر التنفيذ» مش فشل ومش نجاح.
        4 => StepOutcomeTone.Unclear,
        5 => StepOutcomeTone.Unclear,

        6 => StepOutcomeTone.Neutral,

        0 => stepId switch
        {
            // 🔴 صيانة متسجّلة = حاجة محتاجة شغل، فنبرتها وحشة —
            // رغم إن المرحلة نفسها مانجحتش ومافشلتش.
            NotesStep => facts.RepairRecorded ? StepOutcomeTone.Bad : StepOutcomeTone.Neutral,

            ReviewStep => facts.Handed ? StepOutcomeTone.Good : StepOutcomeTone.Neutral,
            SpecsStep => StepOutcomeTone.Neutral,
            _ => StepOutcomeTone.Neutral,
        },

        _ => StepOutcomeTone.Neutral,
    };
}
