using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Audit;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Features.Audit.GetAuditFilters;
using Codlek.Application.Features.Audit.SearchAudit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/audit</c> — سجل المراجعة.
///
/// <para>🔴 <b>للمالك وبس، ودي مقصودة:</b> مدير المخزن هو اللي
/// بيمسح ويعدّل، فلو هو اللي بيراجع تبقى الرقابة بلا معنى.</para>
///
/// <para>🔴 <b>وقراية وبس.</b> مفيش مسار تعديل ولا مسح هنا ولا
/// هيبقى فيه — سجل المراجعة اللي بيتعدّل مش سجل مراجعة. والفحص
/// بالانعكاس بيثبّت إن مفيش إجراء كتابة على الكلاس ده.</para>
/// </summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize(Policies.OwnerOnly)]
public sealed class AuditController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<PagedResult<AuditEventItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? actor,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new SearchAuditQuery(
                from, to, action, entityType, actor, search, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("filters")]
    [ProducesResponseType<AuditFilters>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Filters(CancellationToken ct)
    {
        var result = await sender.Send(new GetAuditFiltersQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
