using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Features.Reports.DeleteReport;
using Codlek.Application.Features.Reports.GetReportDetail;
using Codlek.Application.Features.Reports.GetReportEdits;
using Codlek.Application.Features.Reports.GetReports;
using Codlek.Application.Features.Reports.RestoreReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/reports</c> — الفحوص.
///
/// <para>🔴 <b>القايمة والفحص الواحد بلا سياسة — والفني بيشوف فحوصاته
/// هو.</b> التضييق بيحصل في <b>الاستعلام</b>: القايمة بتتفلتر بكود
/// الفني الحالي، وصفحة الفحص الواحد فيها حارس جوّه المعالج (محتاج
/// يقرا الصف الأول عشان يعرف الفحص بتاع مين).</para>
///
/// <para>⚠️ <b>ولو اتحطّت سياسة مديرين على الكلاس، الفني مايقدرش
/// يفتح شغله هو</b> — وصفحة الفحوص هي أول صفحة بيفتحها. فالسياسات
/// على <b>الإجراءات</b>: المسح والتعديلات للمديرين، والاسترجاع للمالك
/// بس.</para>
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

    /// <summary>
    /// تعديلات الفحص بعد التسليم — الأحدث فوق.
    ///
    /// <para>⚠️ قايمة فاضية = <c>200</c> و<c>[]</c>، والشاشة بتخفي
    /// القسم.</para>
    /// </summary>
    [HttpGet("{id:guid}/edits")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<IReadOnlyList<ReportEditItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edits(Guid id, CancellationToken ct)
    {
        var sent = await sender.Send(new GetReportEditsQuery(id), ct);

        return sent.IsSuccess ? Ok(sent.Value) : sent.ToProblem();
    }

    /// <summary>
    /// مسح فحص بسبب — بعلامة، مش حذف.
    ///
    /// <para>⚠️ <b>الجسم nullable عن قصد:</b> طلب من غير جسم بياخد
    /// رسالة «لازم تكتب سبب المسح» بدل <c>400</c> عام من ASP.NET.</para>
    /// </summary>
    [HttpPost("{id:guid}/delete")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<ReportActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id, [FromBody] DeleteReportRequest? body, CancellationToken ct)
    {
        var sent = await sender.Send(new DeleteReportCommand(id, body?.Reason), ct);

        return sent.IsSuccess ? Ok(sent.Value) : sent.ToProblem();
    }

    /// <summary>
    /// 🔴 <b>استرجاع فحص ممسوح — المالك بس.</b> مدير المخزن هو اللي
    /// بيمسح، فلو هو اللي بيرجّع تبقى المراجعة بلا معنى.
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Policies.OwnerOnly)]
    [ProducesResponseType<ReportActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(
        Guid id, [FromBody] RestoreReportRequest? body, CancellationToken ct)
    {
        var sent = await sender.Send(new RestoreReportCommand(id, body?.Reason), ct);

        return sent.IsSuccess ? Ok(sent.Value) : sent.ToProblem();
    }
}
