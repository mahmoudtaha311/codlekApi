using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Features.Handover.ExecuteHandover;
using Codlek.Application.Features.Handover.GetCandidateIds;
using Codlek.Application.Features.Handover.GetCandidates;
using Codlek.Application.Features.Handover.GetDestinations;
using Codlek.Application.Features.Handover.GetHandoverLog;
using Codlek.Application.Features.Handover.GetRecipients;
using Codlek.Application.Features.Handover.MarkReady;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/handover</c> — تسليم اللابات لجهة.
///
/// <para>🔴 <b>نقطة واحدة بس هي اللي ليها سياسة مختلفة، ودي
/// الميزة.</b> صاحب الشغل قال عن مدير الدور بالنص: «عنده نفس كل
/// حاجة عند المدير بس عنده ميزة زيادة إنه يقدر يسلّم لابات». فتنفيذ
/// التسليم <c>HandoverAllowed</c>، وكل الباقي
/// <c>ManagerOrAbove</c>.</para>
///
/// <para>⚠️ <b>ولو التسليم اتحطّ على <c>ManagerOrAbove</c> يبقى مش
/// ميزة زيادة.</b> الفحص بالانعكاس بيثبّت الفرق ده.</para>
///
/// <para>⚠️ <b>والمراجعة للمديرين وفوق عن قصد:</b> المراجعة
/// والتسليم خطوتين منفصلين — مدير المخزن بيراجع، ومدير الدور
/// بيسلّم.</para>
/// </summary>
[ApiController]
[Route("api/v1/handover")]
[Authorize]
public sealed class HandoverController(ISender sender) : ControllerBase
{
    [HttpGet("destinations")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<IReadOnlyList<HandoverDestination>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Destinations(CancellationToken ct)
    {
        var result = await sender.Send(new GetHandoverDestinationsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("candidates")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<PagedResult<HandoverCandidate>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Candidates(
        [FromQuery] string? search,
        [FromQuery] Guid? container,
        [FromQuery] string? review,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetHandoverCandidatesQuery(search, container, review, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ⚠️ <c>take</c> موجود عشان السقف <b>يتقاس</b> — الواجهة
    /// مابتبعتوش.
    /// </summary>
    [HttpGet("candidate-ids")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<HandoverCandidateIds>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CandidateIds(
        [FromQuery] string? search,
        [FromQuery] Guid? container,
        [FromQuery] string? review,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetHandoverCandidateIdsQuery(search, container, review, take), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// المراجعة — <b>الحكم البني آدمي اللي الكمبيوتر مش قادر
    /// عليه</b>.
    /// </summary>
    [HttpPost("ready")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<HandoverReadyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ready(
        [FromBody] HandoverReadyRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new MarkHandoverReadyCommand(body.DeviceIds, body.Ready), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("recipients")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<IReadOnlyList<HandoverRecipientItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Recipients(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await sender.Send(new GetHandoverRecipientsQuery(from, to), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// 🔴 <b>دي النقطة الوحيدة في النظام كله بسياسة
    /// <c>HandoverAllowed</c>.</b> هي الميزة اللي مدير الدور عنده
    /// والمدير مالوش.
    /// </summary>
    [HttpPost("")]
    [Authorize(Policies.HandoverAllowed)]
    [ProducesResponseType<HandoverResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Execute(
        [FromBody] HandoverRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new ExecuteHandoverCommand(
                body.DeviceIds,
                body.DestinationId,
                body.ReceivedByName,
                body.Reason,
                body.Notes,
                body.OverrideReason), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("log")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<PagedResult<HandoverLogItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Log(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? destination,
        [FromQuery] string? receiver,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetHandoverLogQuery(from, to, destination, receiver, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
