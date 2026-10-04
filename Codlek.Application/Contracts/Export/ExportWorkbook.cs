using Codlek.Core.Spreadsheets;

namespace Codlek.Application.Contracts.Export;

/// <summary>
/// ملف تصدير جاهز — <b>اسم وصفحات، من غير بايتات</b>.
///
/// <para>🔴 <b>والمعالج مابيكتبش ملف.</b> طبقة التطبيق بتبني أعمدة
/// وصفوف؛ الكنترولر هو اللي بينده الكاتب ويصبّ على الرد. ولو
/// المعالج رجّع <c>Stream</c>، كل فحص وحدة على التصدير كان لازم
/// يفك ملف ZIP عشان يشوف صف.</para>
///
/// <para>⚠️ <see cref="FileName"/> من غير امتداد — الامتداد بييجي
/// من الكاتب.</para>
/// </summary>
public sealed record ExportWorkbook(string FileName, IReadOnlyList<Sheet> Sheets);
