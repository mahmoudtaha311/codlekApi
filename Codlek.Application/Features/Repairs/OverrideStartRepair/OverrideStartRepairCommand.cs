using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.OverrideStartRepair;

/// <summary>
/// تجاوز إداري لبدء الصيانة.
///
/// <para>⚠️ <b><c>Reason</c> بيفضل اختياري هنا وبيتفحص في
/// المعالج.</b> لأن ترتيب الرفض مهم: المالك الأول، وبعدين السبب،
/// وبعدين فحوص الأمر. والمتحقّق بيشتغل قبل المعالج فكان هيقلب
/// الترتيب.</para>
/// </summary>
public sealed record OverrideStartRepairCommand(Guid Id, Guid TechnicianId, string? Reason)
    : IRequest<Result<RepairActionResponse>>;
