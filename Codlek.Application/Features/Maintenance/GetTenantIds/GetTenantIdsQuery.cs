using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Features.Maintenance.GetTenantIds;

/// <summary>
/// كل الشركات — صيانة الإقلاع بتمشي على كل واحدة لوحدها.
///
/// <para>🔴 <b>للنظام بس، مش للوحة.</b> مفيش كنترولر بينده ده: قايمة
/// الشركات مالهاش معنى لمستخدم شركته معروفة من توكنه.</para>
/// </summary>
public sealed record GetTenantIdsQuery : IRequest<Result<IReadOnlyList<Guid>>>;
