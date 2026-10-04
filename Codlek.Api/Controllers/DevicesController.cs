using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Features.Devices.GetDevices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/devices</c> — قايمة اللابات.
///
/// <para>⚠️ <b>فيه تلات كنترولرات على نفس البادئة دي، وده
/// مقصود:</b> ده للقايمة، و<see cref="DeviceRepairsController"/>
/// لتبويب الصيانة، و<see cref="HardwareController"/> للعتاد. التقسيم
/// <b>بالقطاع</b> مش بالبادئة — زي ما القديم كان مقسّم على ملفات
/// (<c>ApiV1.Hardware.cs</c> · <c>ApiV1.Repairs.cs</c>). وASP.NET
/// بيجمعهم على نفس المسار عادي لأن قوالب الإجراءات مابتتعارضش.</para>
///
/// <para>⚠️ <b>والقايمة <c>ManagerOrAbove</c>:</b> الفني بيشوف
/// <b>فحوصاته</b>، مش قايمة لابات الورشة.</para>
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class DevicesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ⚠️ <b>كل الفلاتر «أحسن مجهود».</b> القيمة المش مفهومة بتتجاهل
    /// — دي مدخلات من رابط محفوظ في المتصفح، والرد عمره ما بيبقى
    /// <c>400</c>.
    /// </summary>
    [HttpGet("")]
    [ProducesResponseType<PagedResult<DeviceListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? confidence,
        [FromQuery] string? outcome,
        [FromQuery] string? technician,
        [FromQuery] Guid? rack,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? stage,
        [FromQuery] string? sort,
        [FromQuery] Guid? container,
        [FromQuery] string? flag,
        [FromQuery] string? handover,
        [FromQuery] Guid? location,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetDevicesQuery(
                search, status, confidence, outcome, technician, rack, from, to,
                stage, sort, container, flag, handover, location, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
