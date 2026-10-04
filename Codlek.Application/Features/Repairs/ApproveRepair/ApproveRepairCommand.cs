using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.ApproveRepair;

/// <summary>
/// موافقة المحاسب — <b>ومعاها تغيير الفني لو لزم</b>.
///
/// <para>🔴 <b>الحقلين اختياريين ومفيش متحقّق عليهم.</b> فحوص المشروع
/// القديم بتبعت جسم فاضي <c>{}</c> وبتتوقّع <c>404</c> — يعني السياسة
/// عدّت والكود دوّر على الصف. وأي قاعدة <c>NotEmpty</c> بتحوّل ده
/// لـ<c>400</c> والفحص بيقرا إن الحاجز مكسور وهو سليم.</para>
/// </summary>
public sealed record ApproveRepairCommand(Guid Id, Guid? TechnicianId, string? Note)
    : IRequest<Result<RepairActionResponse>>;
