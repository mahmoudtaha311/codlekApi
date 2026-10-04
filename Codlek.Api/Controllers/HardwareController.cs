using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Hardware;
using Codlek.Application.Features.Hardware.CompareDeviceSnapshots;
using Codlek.Application.Features.Hardware.GetDeviceHardware;
using Codlek.Application.Features.Hardware.GetReportHardware;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// العتاد — لقطة فحص واحد، ومقارنة فحصين على نفس الجهاز.
///
/// <para>🔴 <b>الحساب كله على السيرفر، وممنوع إعادة تنفيذ المقارنة
/// في الواجهة.</b> القواعد دي بتتحوّل لاتهام إن موظف غيّر قطعة.
/// نسخة تانية منها في الواجهة معناها نسختين من الحكم، وواحدة منهم
/// هتختلف عن التانية أول ما حد يعدّل واحدة بس.</para>
///
/// <para>🔴 <b>والسياسات هنا تلات مستويات مختلفة، وده مقصود:</b>
/// لقطة الفحص «مسجّل دخول» + حارس جوّه المعالج (الفني يشوف شغله
/// هو)، ولقطة الجهاز والمقارنة <c>ManagerOrAbove</c>. وفي المشروع
/// القديم كانت سياسة المقارنة <b>ناقصة</b> — المسار كان متسجّل على
/// المجموعة الجذر، فأي فني كان بيقدر يقارن لقطات أي جهاز في الشركة
/// ويقرا سيريالات بضاعة مش شغله.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class HardwareController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ⚠️ <b>النقطة الوحيدة في العتاد اللي الفني بيوصلها.</b>
    /// والسياسة هنا «مسجّل دخول» بس — الحارس الحقيقي جوّه المعالج،
    /// لأنه محتاج يقرا الصف الأول عشان يعرف الفحص بتاع مين.
    /// </summary>
    [HttpGet("api/v1/reports/{id:guid}/hardware")]
    [ProducesResponseType<ReportHardwareResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForReport(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetReportHardwareQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("api/v1/devices/{deviceId:guid}/hardware")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<ReportHardwareResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForDevice(Guid deviceId, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceHardwareQuery(deviceId), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// مقارنة لقطتين.
    ///
    /// <para>⚠️ <c>left</c> و<c>right</c> من سلسلة الاستعلام،
    /// <b>واختياريين</b>: الواجهة بتنده النقطة من غيرهم أول ما
    /// الصفحة تفتح، والرد لازم يبقى <c>400</c> برسالة مفهومة مش
    /// <c>404</c>.</para>
    /// </summary>
    [HttpGet("api/v1/devices/{deviceId:guid}/compare")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<CompareResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compare(
        Guid deviceId,
        [FromQuery] Guid? left,
        [FromQuery] Guid? right,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CompareDeviceSnapshotsQuery(deviceId, left, right), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
