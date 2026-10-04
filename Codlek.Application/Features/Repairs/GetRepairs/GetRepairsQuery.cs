using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetRepairs;

/// <summary>
/// قايمة أوامر الصيانة.
///
/// <para>🔴 <b>مفيش متحقّق على الاستعلام ده — ولا واحد.</b> كل فلتر
/// «أحسن مجهود» عن قصد: الحالة المش مفهومة بتتجاهل، والصفحة بتتظبّط
/// في المدى، والرد عمره ما بيبقى <c>400</c>. الداش بورد بتحفظ
/// الفلاتر في الرابط، فأي قيمة قديمة في رابط محفوظ كانت بتفضّي
/// الشاشة.</para>
/// </summary>
public sealed record GetRepairsQuery(
    string? Search,
    string? Status,
    string? Approval,
    Guid? Technician,
    DateTime? From,
    DateTime? To,
    string? Sort,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<RepairListItem>>>;
