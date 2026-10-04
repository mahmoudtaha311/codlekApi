using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// صف فحص في أي قايمة.
///
/// <para>⚠️ <b>اسم الفني هنا لقطة وقت الفحص، مش الاسم الحالي.</b>
/// ومعاه المعرّف عشان الواجهة تقدر تشاور على الحساب نفسه من غير ما
/// تعتمد على الكود كمفتاح — والمدير لما يصحّح اسم فني، التقارير
/// القديمة مالهاش تتغيّر.</para>
/// </summary>
/// <param name="DevicePublicCode">
/// 🔴 كود الجهاز <b>المربوط</b> هو الأصل، واللي في الفحص احتياطي
/// للفحص اللي لسه مش مربوط.
/// </param>
public sealed record ReportListItem(
    Guid Id,
    Guid? DeviceId,
    string DevicePublicCode,
    string Manufacturer,
    string Model,
    string CommercialModelName,
    string Cpu,
    string RamText,
    string StorageText,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    string RackName,
    DateTime StartedAtUtc,
    DateTime ReceivedAtUtc,
    long DurationMs,
    TestCounts Counts,
    int? Scope,
    string ScopeText,
    int NotRunCount);
