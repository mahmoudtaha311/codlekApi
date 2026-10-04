using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Audit;
using Codlek.Application.Contracts.Common;
using MediatR;

namespace Codlek.Application.Features.Audit.SearchAudit;

/// <summary>
/// سجل المراجعة — <b>قراية وبس</b>.
///
/// <para>🔴 <b>مفيش مسار تعديل ولا مسح هنا ولا هيبقى فيه.</b> سجل
/// المراجعة اللي بيتعدّل مش سجل مراجعة.</para>
///
/// <para>⚠️ <b>وللمالك وبس</b> — ودي مقصودة: مدير المخزن هو اللي
/// بيمسح ويعدّل، فلو هو اللي بيراجع تبقى الرقابة بلا معنى.</para>
/// </summary>
public sealed record SearchAuditQuery(
    DateTime? From,
    DateTime? To,
    string? Action,
    string? EntityType,
    string? Actor,
    string? Search,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<AuditEventItem>>>;
