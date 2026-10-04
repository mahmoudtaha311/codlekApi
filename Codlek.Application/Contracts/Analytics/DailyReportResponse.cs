using Codlek.Application.Contracts.Reports;

namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// تقرير يوم واحد.
///
/// <para>⚠️ <b>مش نسخة من اللوحة بفترة يوم.</b> الفرق إن ده بيرجّع
/// كمان القطع المستخدمة، وعدد اللي اتمسح في اليوم، وساعات الشغل —
/// أرقام بتتسلّم للإدارة ومالهاش معنى على فترة ممتدة.</para>
/// </summary>
/// <param name="NextDay">
/// ⚠️ <c>null</c> لو اليوم ده هو النهاردة — بكرة مالوش بيانات،
/// وزرار «اليوم اللي بعده» لازم يتقفل.
/// </param>
/// <param name="DeletedCount">
/// 🔴 <b>للمديرين وفوق — بيرجع <c>0</c> للفني.</b> ده رقم إداري
/// («المدير مسح كام فحص النهاردة») وكان بيتحسب على الشركة كلها من
/// غير تضييق، فالفني كان بيشوفه جمب صفحة بتقول له «بتشوف فحوصاتك
/// إنت بس».
/// </param>
public sealed record DailyReportResponse(
    PeriodInfo Period,
    string DayLabel,
    string PreviousDay,
    string? NextDay,
    DashboardKpis Kpis,
    TestCounts Counts,
    int DeletedCount,
    double TotalHours,
    IReadOnlyList<TrendPoint> Hourly,
    IReadOnlyList<TechnicianActivityItem> Technicians,
    IReadOnlyList<RackActivityItem> Racks,
    IReadOnlyList<NamedCountItem> Failures,
    IReadOnlyList<NamedCountItem> Parts,
    IReadOnlyList<ReportListItem> Attention,
    IReadOnlyList<ReportListItem> Recent);
