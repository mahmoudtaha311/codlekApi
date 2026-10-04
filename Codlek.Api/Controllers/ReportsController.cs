using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Features.Reports.GetReportDetail;
using Codlek.Application.Features.Reports.GetReports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/reports</c> — الفحوص.
///
/// <para>🔴 <b>بلا سياسة على أي إجراء — والفني بيشوف فحوصاته
/// هو.</b> التضييق بيحصل في <b>الاستعلام</b>: القايمة بتتفلتر بكود
/// الفني الحالي، وصفحة الفحص الواحد فيها حارس جوّه المعالج (محتاج
/// يقرا الصف الأول عشان يعرف الفحص بتاع مين).</para>
///
/// <para>⚠️ <b>ولو اتحطّت سياسة مديرين هنا، الفني مايقدرش يفتح
/// شغله هو</b> — وصفحة الفحوص هي أول صفحة بيفتحها.</para>
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<PagedResult<ReportListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? result,
        [FromQuery] string? technician,
        [FromQuery] Guid? container,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var sent = await sender.Send(
            new GetReportsQuery(
                search, result, technician, container, from, to, page, pageSize), ct);

        return sent.IsSuccess ? Ok(sent.Value) : sent.ToProblem();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReportDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var sent = await sender.Send(new GetReportDetailQuery(id), ct);

        return sent.IsSuccess ? Ok(sent.Value) : sent.ToProblem();
    }
}
