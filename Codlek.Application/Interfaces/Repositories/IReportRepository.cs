using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// الفحوص.
///
/// <para>🔴 <b>الممسوح مستبعد من كل قراية هنا <u>ما عدا</u> فحص
/// واحد بمعرّفه.</b> القايمة والتجميعات مابتشوفش الممسوح — لو الفني
/// مسح فحص المفروض العدد يقل. لكن صفحة الفحص الواحد <b>بتفتحه</b>
/// ومعاها سبب المسح ومين مسحه: الصف ده دليل، وإخفاؤه بيخلّي سجل
/// المراجعة يشاور على صفحة ميتة.</para>
///
/// <para>⚠️ <b>والتضييق على الفني جوّه كل دالة.</b> مفيش فلتر عام
/// بيفرضه.</para>
/// </summary>
public interface IReportRepository
{
    /// <summary>صفحة من القايمة + العدد الكلي قبل التصفيح.</summary>
    Task<(IReadOnlyList<Report> Rows, int TotalItems)> ListAsync(
        Guid tenantId, ReportListFilter filter, CancellationToken ct = default);

    /// <summary>نفس الفلتر بلا تصفيح — للتصدير.</summary>
    /// <param name="cap">
    /// 🔴 <b>سقف الصفوف — والمستودع بيجيب <c>cap + 1</c>.</b>
    ///
    /// <para>الصف الزيادة هو اللي بيخلّي المنادي يعرف إن فيه قص
    /// ويقوله <b>جوّه الملف</b>. ملف مقصوص في صمت بيتقري على إنه كل
    /// البيانات — والمدير بيبني عليه قرار جرد.</para>
    /// </param>
    Task<IReadOnlyList<Report>> ExportAsync(
        Guid tenantId, ReportListFilter filter, int cap, CancellationToken ct = default);

    /// <summary>
    /// فحص واحد بمعرّفه — <b>ومعاه المراحل والقطع</b>.
    ///
    /// <para>⚠️ <b>وبيرجّع الممسوح كمان.</b> صفحة الفحص بتعرض سبب
    /// المسح ومين مسحه وإمتى — والصف ده دليل، مش زبالة.</para>
    /// </summary>
    Task<Report?> FindDetailAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>كام قطعة في لقطة عتاد الفحص ده.</summary>
    Task<int> SnapshotComponentCountAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// نسخ البرنامج وتعريف الفحوص — <b>من الحمولة الخام</b>.
    ///
    /// <para>🔴 <b>بـ<c>JSON_VALUE</c> في SQL، مش بقراية في
    /// الذاكرة.</b> الحمولة الخام حوالي <b>٢٠ كيلوبايت للفحص
    /// الواحد</b> — صفحة تاريخ فيها ٢٥ فحص كانت هتسحب نص مليون بايت
    /// عشان تقرا كلمتين.</para>
    ///
    /// <para>🔴 <b>و<c>ISJSON</c> مش رفاهية:</b> <c>JSON_VALUE</c>
    /// على نص مش JSON سليم <b>بيرمي</b> — يعني صف واحد بايظ كان
    /// هيوقّع الصفحة كلها.</para>
    /// </summary>
    Task<ReportVersionFacts?> VersionsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>أكواد الراكات — قراية واحدة للصفحة.</summary>
    Task<IReadOnlyDictionary<Guid, RackLabel>> RackLabelsAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>أكواد الأجهزة — قراية واحدة للصفحة.</summary>
    Task<IReadOnlyDictionary<Guid, string>> DeviceCodesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);
}
