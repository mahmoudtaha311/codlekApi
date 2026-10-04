using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Features.Repairs.GetDeviceRepairs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/devices/{id}/repairs</c> — تبويب الصيانة في صفحة
/// اللاب.
///
/// <para>🔴 <b>كنترولر لوحده لأن المسار على بادئة الأجهزة مش على
/// بادئة الصيانة.</b> الداش بورد بتنده العنوان ده بالحرف، وحرفٌ
/// واحد مختلف معناه <c>404</c> على تبويب شغّال.</para>
///
/// <para>⚠️ <b>والصلاحية هنا أضيق من قايمة الصيانة:</b>
/// <c>ManagerOrAbove</c> مش <c>RepairsViewer</c> — المحاسب برّه.
/// المشروع القديم كان بيفحصها <b>مرتين</b> (سياسة المسار وفحص جوّه
/// المعالج)؛ واحدة كفاية، بس <b>المجموعة</b> ماتتوسّعش.</para>
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class DeviceRepairsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/repairs")]
    [ProducesResponseType<IReadOnlyList<RepairListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceRepairsQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
