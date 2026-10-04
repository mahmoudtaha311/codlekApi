using Codlek.Core.Analytics;
using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// إنتاجية الفنيين.
///
/// <para>🔴 <b>مفتاحين مختلفين لنفس الشخص — والجسر بينهم هنا.</b>
/// الفحص مفتاحه <b>كود نص</b> (الراكة بتكتبه)، وأمر الصيانة مفتاحه
/// <b><c>Guid</c></b> (جدول الفنيين). فالقراية بترجّع الاتنين بالكود
/// عشان المنادي يضمّهم من غير ما يعرف بالفخ ده.</para>
///
/// <para>🔴 <b>والتجميع بينزل SQL — ده مش تفضيل أسلوب.</b> الفترة
/// الافتراضية للشاشة دي «من البداية»، فسحب صف لكل فحص أو لكل أمر
/// قبل التجميع معناه <b>الجدول كله</b> في ذاكرة السيرفر في كل فتحة
/// للصفحة. واللي بيرجع من هنا صف لكل فني — عشرات، مش آلاف.</para>
/// </summary>
public interface ITechnicianProductivityRepository
{
    /// <summary>
    /// إنتاجية الفحص لكل فني في الفترة.
    /// </summary>
    Task<IReadOnlyList<TestingProductivityFacts>> TestingAsync(
        Guid tenantId, AnalyticsPeriod window, CancellationToken ct = default);

    /// <summary>
    /// إنتاجية الصيانة لكل فني في الفترة — <b>مفتاحها الكود</b>.
    ///
    /// <para>⚠️ والأمر اللي مالوش «اللي قفله» بيرجع للمتسند —
    /// الصفوف القديمة كانت بتقفل من غير ما تسجّل مين قفلها.</para>
    /// </summary>
    Task<IReadOnlyDictionary<string, RepairProductivityFacts>> RepairsAsync(
        Guid tenantId, AnalyticsPeriod window, CancellationToken ct = default);

    /// <summary>
    /// فيه فحص واحد على الأقل بالكود ده؟ — <b>في كل التاريخ</b>.
    ///
    /// <para>⚠️ مش في المدى المختار: صفحة فني آخر شغلانة ليه من تلات
    /// شهور لازم تفتح، وإلا بيختفي من النظام وهو لسه موظّف.</para>
    /// </summary>
    Task<bool> HasAnyReportAsync(
        Guid tenantId, string code, CancellationToken ct = default);

    /// <summary>
    /// اسم الفني من <b>أحدث فحص فيه اسم</b> — في كل التاريخ.
    ///
    /// <para>🔴 <b>من دليل المحطة، مش من جدول الحسابات.</b> فيه
    /// فنيين بيشتغلوا على الراكة ومالهمش حساب في اللوحة خالص؛ ولو
    /// الاسم اتقرا من الجدول، صفحتهم كانت بتفضل بلا اسم للأبد.</para>
    /// </summary>
    Task<string?> NameFromReportsAsync(
        Guid tenantId, string code, CancellationToken ct = default);

    /// <summary>أرقام فني واحد في الفترة.</summary>
    Task<TestingProductivityFacts?> OneAsync(
        Guid tenantId, string code, AnalyticsPeriod window, CancellationToken ct = default);

    /// <summary>آخر فحوص الفني في الفترة.</summary>
    Task<IReadOnlyList<Report>> RecentAsync(
        Guid tenantId, string code, AnalyticsPeriod window, int take,
        CancellationToken ct = default);
}
