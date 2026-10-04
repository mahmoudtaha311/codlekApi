namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// مرحلة في الفحص.
/// </summary>
/// <param name="StatusText">
/// 🔴 <b>مش ترجمة الرقم على طول.</b> تلات مراحل مالهاش نتيجة فحص
/// أصلاً (المواصفات والملاحظات والتسليم) — وكانوا بيطلعوا «لم
/// يُنفّذ» على <b>كل فحص متسلّم</b>. راجع
/// <c>StageOutcomeText</c>.
/// </param>
/// <param name="Outcome">
/// ⚠️ نبرة للعرض: <c>good</c> · <c>bad</c> · <c>unclear</c> ·
/// <c>neutral</c>. و«مش موجود» و«تعذّر التنفيذ» بياخدوا
/// <c>unclear</c> — لا نجاح ولا فشل.
/// </param>
public sealed record ReportStepItem(
    string Title,
    string Status,
    string StatusText,
    string Outcome,
    string Note,
    string SkipReason,
    string Detail,
    long DurationMs);
