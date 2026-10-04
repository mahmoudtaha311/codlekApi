namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// فتح أمر صيانة.
///
/// <para>⚠️ <c>RequiredSpecialty</c> رقم <c>TechnicianSpecialty</c> —
/// و<b>بيتفحص قبل الكاست</b>. القديم كان بيعمل الكاست على الناشف،
/// فـ<c>99</c> كان بيتخزّن والأمر يفضل بـ«غير محدد» للأبد.</para>
/// </summary>
public sealed record OpenRepairRequest(
    Guid DeviceId,
    Guid? SourceReportId,
    Guid? AssignTechnicianId,
    string? FaultSummary,
    int? RequiredSpecialty);
