using Codlek.Application.Contracts.Reports;

namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// اللوحة الرئيسية.
///
/// <para>⚠️ <c>Attention</c> قبل <c>Recent</c>: اللي محتاج مراجعة
/// هو اللي المدير بيفتح الصفحة عشانه.</para>
/// </summary>
public sealed record DashboardResponse(
    PeriodInfo Period,
    DashboardKpis Kpis,
    TestCounts Counts,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<ReportListItem> Attention,
    IReadOnlyList<ReportListItem> Recent);
