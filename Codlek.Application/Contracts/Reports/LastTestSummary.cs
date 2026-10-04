using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// ملخّص آخر فحص للاب.
///
/// <para>⚠️ <b>اسم الفني لقطة وقت الفحص، مش الاسم الحالي.</b>
/// ومعاه المعرّف عشان الواجهة تشاور على الحساب نفسه من غير ما تعتمد
/// على الكود كمفتاح — والمدير لما يصحّح اسم فني، التقارير القديمة
/// مالهاش تتغيّر.</para>
/// </summary>
public sealed record LastTestSummary(
    Guid ReportId,
    DateTime StartedAtUtc,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    TestCounts Counts);
