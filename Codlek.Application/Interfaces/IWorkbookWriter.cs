using Codlek.Core.Spreadsheets;

namespace Codlek.Application.Interfaces;

/// <summary>
/// بيحوّل الصفحات لملف.
///
/// <para>🔴 <b>وليه بورت.</b> طبقة التطبيق بتبني أعمدة وصفوف وخلاص؛
/// الصيغة (ZIP + XML بشكل OOXML) تفصيلة بنية تحتية. ولو كل معالج
/// نده الكاتب مباشرةً، تغيير الصيغة كان هيلمس سبع معالجات.</para>
/// </summary>
public interface IWorkbookWriter
{
    /// <summary>بيكتب الملف كله على المجرى.</summary>
    void Write(Stream output, IReadOnlyList<Sheet> sheets);

    /// <summary>نوع المحتوى اللي المتصفح بيستقبله.</summary>
    string ContentType { get; }

    /// <summary>امتداد الملف — من غير نقطة.</summary>
    string Extension { get; }
}
