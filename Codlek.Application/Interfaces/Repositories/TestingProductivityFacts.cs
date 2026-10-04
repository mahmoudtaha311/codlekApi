namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// إنتاجية فحص فني واحد — <b>خام ومجمّعة في SQL</b>.
/// </summary>
/// <param name="Name">
/// ⚠️ <b>من أحدث فحص فيه اسم</b> — دليل المحطة، مش جدول الحسابات.
/// وممكن يبقى فاضي: فيه فحوص قديمة اسمها مش مكتوب.
/// </param>
/// <param name="Timed">
/// ⚠️ عدد الفحوص اللي ليها مدة — المقام بيتعدّ لوحده عشان الفحوص
/// اللي مدّتها صفر ماتنقّصش المتوسط.
/// </param>
public sealed record TestingProductivityFacts(
    string Code,
    string Name,
    int Total,
    int Pass,
    int Fail,
    int Error,
    int NotPresent,
    int Skip,
    int Timed,
    long DurationMs,
    DateTime? LastAtUtc);
