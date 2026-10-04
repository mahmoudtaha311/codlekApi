using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.SetBrands;

/// <summary>
/// ماركات الفني — <b>استبدال كامل</b>.
///
/// <para>🔴 <b>مسار لوحده مش حقل في «تعديل الفني».</b> مسار التعديل
/// بيفرّق بين «ماتلمسش» و«شيل» بقيمة حارسة لأنه بيعدّل حقول تانية —
/// ومجموعة مالهاش قيمة حارسة تنفع. فالاستبدال الكامل هنا، والقايمة
/// الفاضية معناها «مفيش قيد» بقرار صريح.</para>
/// </summary>
public sealed record SetTechnicianBrandsCommand(Guid Id, List<Guid>? BrandIds)
    : IRequest<Result<TechnicianBrandsResponse>>;
