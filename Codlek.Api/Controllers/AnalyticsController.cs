using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Features.Analytics.GetAlerts;
using Codlek.Application.Features.Analytics.GetDailyReport;
using Codlek.Application.Features.Analytics.GetDashboard;
using Codlek.Application.Features.Analytics.GetDashboardBreakdown;
using Codlek.Application.Features.Analytics.GetInventory;
using Codlek.Application.Features.Analytics.GetPartsDemand;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// التحليلات — اللوحة، الجرد، قطع الغيار، وتقرير اليوم.
///
/// <para>🔴 <b>تلات مستويات صلاحية، وكل واحد بيعني حاجة
/// مختلفة:</b></para>
/// <list type="bullet">
///   <item><b>بلا سياسة</b> (اللوحة · التفصيل · تقرير اليوم) —
///         الفني بيشوفها <b>مضيّقة على شغله</b>، والتضييق في
///         السيرفر مش في الواجهة.</item>
///   <item><b>بلا سياسة وبترجّع أصفار</b> (التحذيرات) — الأيقونة
///         بتتنده على كل صفحة، و<c>403</c> كان بيطلّع رسالة خطأ في
///         ترويسة صفحة الفني كل مرة.</item>
///   <item><c>ManagerOrAbove</c> (الجرد · قطع الغيار) — أرقام على
///         مستوى الشركة مالهاش نسخة «بتاعتي».</item>
/// </list>
///
/// <para>⚠️ <b>والمسارات مش على بادئة واحدة</b> (<c>/dashboard</c>
/// و<c>/alerts/...</c> و<c>/inventory/...</c> و<c>/parts-demand</c>
/// و<c>/daily-report</c>)، فمفيش <c>[Route]</c> على الكلاس وكل
/// إجراء بيكتب مساره كامل.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class AnalyticsController(ISender sender) : ControllerBase
{
    [HttpGet("api/v1/dashboard")]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(
        [FromQuery] string? range,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetDashboardQuery(range, from, to), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("api/v1/dashboard/breakdown")]
    [ProducesResponseType<DashboardBreakdownResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Breakdown(
        [FromQuery] string? range,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetDashboardBreakdownQuery(range, from, to), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ⚠️ <b>بلا سياسة، وبترجّع أصفار للفني.</b> دي بتتنده على كل
    /// صفحة — راجع التعليق على الكلاس.
    /// </summary>
    [HttpGet("api/v1/alerts/summary")]
    [ProducesResponseType<AlertsSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Alerts(CancellationToken ct)
    {
        var result = await sender.Send(new GetAlertsSummaryQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("api/v1/inventory/summary")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<InventorySummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Inventory(CancellationToken ct)
    {
        var result = await sender.Send(new GetInventorySummaryQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("api/v1/parts-demand")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<PartsDemandResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PartsDemand(
        [FromQuery] string? range,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetPartsDemandQuery(range, from, to, limit), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("api/v1/daily-report")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DailyReport(
        [FromQuery] DateTime? day, CancellationToken ct)
    {
        var result = await sender.Send(new GetDailyReportQuery(day), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
