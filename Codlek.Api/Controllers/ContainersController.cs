using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Containers;
using Codlek.Application.Features.Containers.CreateContainer;
using Codlek.Application.Features.Containers.GetContainer;
using Codlek.Application.Features.Containers.GetContainers;
using Codlek.Application.Features.Containers.UpdateContainer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/containers</c> — حاويات الاستيراد.
///
/// <para>🔴 <b>الحاوية صفة ثابتة للاب، مش مكان بيتنقل بينه.</b> صاحب
/// الشغل وضّحها: «اللاب كان جاي من الاستيراد فيها، وبتاخد رمز ومش
/// بتتغير». عشان كده الربط عمود واحد على <c>Device</c> بيتكتب مرة
/// واحدة — مفيش جدول حركة ولا تاريخ نقل.</para>
/// </summary>
[ApiController]
[Route("api/v1/containers")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class ContainersController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<ContainerListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, CancellationToken ct)
    {
        var result = await sender.Send(new GetContainersQuery(search), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ContainerDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetContainerQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    [ProducesResponseType<ContainerListItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] SaveContainerRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateContainerCommand(
                body.Code ?? "", body.Name ?? "", body.SortOrder ?? 0), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// تعديل حاوية — <b>والرمز بيتجاهل</b>.
    ///
    /// <para>🔴 الواجهة ممكن تبعت <c>code</c> في الجسم (نفس العقد
    /// بيتستعمل للإنشاء والتعديل)، والنقطة دي <b>بتسيبه</b>. مش غلط
    /// ولا سهو: الرمز مطبوع على شحنة حقيقية.</para>
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ContainerListItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SaveContainerRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateContainerCommand(id, body.Name, body.SortOrder, body.IsActive), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
