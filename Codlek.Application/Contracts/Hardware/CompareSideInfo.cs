namespace Codlek.Application.Contracts.Hardware;

/// <summary>ترويسة جهة واحدة في المقارنة.</summary>
/// <param name="ComponentCount">
/// ⚠️ <b>بيتحسب من القايمة المحمّلة، مش من استعلام عدّ.</b>
/// القايمتين متحمّلين في الذاكرة أصلاً عشان المقارنة، فالعد من
/// عندهم مجاني وبيوصف <b>اللي اتقارن فعلاً</b>.
///
/// <para>🔴 وفي القديم كان فيه فخّ هنا: ترويسة الفحص الواحد
/// مابتعدّش المكوّنات (عكس ترويسات الصفحة الكاملة)، فقراية
/// <c>ComponentCount</c> من الترويسة كانت بتطلّع <b>صفر
/// دايماً</b> — والواجهة بتعرض «المكوّنات: ٠» جمب جدول فيه عشرين
/// صف.</para>
/// </param>
public sealed record CompareSideInfo(
    Guid ReportId,
    DateTime StartedAtUtc,
    DateTime? CapturedAtUtc,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    int ComponentCount,
    bool IsPartial,
    bool RanAsAdministrator);
