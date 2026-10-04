using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.RejectRepair;

/// <summary>
/// رفض المحاسب.
///
/// <para>⚠️ نفس عقد الموافقة بيتستعمل في الطلب،
/// و<c>TechnicianId</c> <b>بيتجاهل</b> هنا — مفيش معنى لتغيير فني على
/// أمر مرفوض.</para>
/// </summary>
public sealed record RejectRepairCommand(Guid Id, string? Note)
    : IRequest<Result<RepairActionResponse>>;
