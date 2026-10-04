using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetCandidates;

/// <summary>
/// اللابات اللي ينفع تتسلّم.
///
/// <para>🔴 <b><c>review</c> هو اللي بيفصل بين تلات شاشات:</b>
/// <c>pending</c> (مستني مراجعة) · <c>ready</c> (اتراجع — وده
/// الافتراضي) · <c>all</c> (للبحث الاستثنائي بالكود).</para>
///
/// <para>⚠️ والافتراضي <c>ready</c> عن قصد: لو كان <c>all</c>، صفحة
/// التسليم كانت هترجع لسلوكها القديم بالظبط والمراجعة تبقى شاشة
/// مالهاش أثر.</para>
/// </summary>
public sealed record GetHandoverCandidatesQuery(
    string? Search,
    Guid? Container,
    string? Review,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<HandoverCandidate>>>;
