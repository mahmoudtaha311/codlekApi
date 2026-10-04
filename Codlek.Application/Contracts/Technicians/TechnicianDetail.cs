using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Reports;

namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// صفحة فني واحد.
///
/// <para>⚠️ <b>الاسم بيتحل من كل تاريخ الفني، مش من المدى
/// المختار</b> — اختيار أسبوع فاضي مالوش يضيّع اسم الصفحة.</para>
/// </summary>
public sealed record TechnicianDetail(
    string Code,
    string Name,
    bool NameAvailable,
    int TotalReports,
    TestCounts Counts,
    double AverageMinutes,
    DateTime? LastAtUtc,
    IReadOnlyList<ReportListItem> Recent);
