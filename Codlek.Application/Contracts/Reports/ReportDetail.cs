using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// فحص واحد بكل تفاصيله.
/// </summary>
/// <param name="ReceivedAtUtc">
/// 🔴 <b>وقت السيرفر — وده اللي الترتيب بيمشي عليه.</b>
/// <c>StartedAtUtc</c> بتيجي من الراكة وساعتها مش موثوقة (راكة في
/// الميدان ساعتها كانت مقدّمة ٥٧ دقيقة).
/// </param>
/// <param name="ApplicationVersion">
/// ⚠️ بتتقرا من الحمولة الخام بـ<c>JSON_VALUE</c> — مش عمود.
/// والناقصة بترجع «غير متاح».
/// </param>
/// <param name="SnapshotComponentCount">
/// ⚠️ عدد قطع لقطة العتاد — <c>0</c> معناها إن الفحص ده مالوش
/// لقطة، والشاشة بتخفي تبويب العتاد.
/// </param>
public sealed record ReportDetail(
    Guid Id,
    Guid? DeviceId,
    string DevicePublicCode,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    string RackName,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    DateTime ReceivedAtUtc,
    long DurationMs,
    TestCounts Counts,
    ReportSpecs Specs,
    IReadOnlyList<ReportStepItem> Steps,
    IReadOnlyList<string> PartsUsed,
    string GeneralNote,
    bool IsDeleted,
    string DeletedReason,
    string DeletedByName,
    DateTime? DeletedAtUtc,
    string ApplicationVersion,
    string TestDefinitionVersion,
    int SnapshotComponentCount,
    int? Scope,
    string ScopeText,
    int NotRunCount);
