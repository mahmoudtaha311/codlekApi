using Codlek.Api.Racks;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Contracts.Wire;
using Codlek.Application.Features.Rack.Feeds;
using Codlek.Core.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codlek.Api.Controllers;

/// <summary>
/// التغذيات النازلة — <b>أول قناة بتمشي من السيرفر للراكة</b>.
///
/// <para>🔴 <b>وقبل كده المزامنة كانت رفع بس.</b> أمر الصيانة
/// بيتفتح على راكة الفحص وبيتسند لفني صيانة بيشتغل على راكة تانية —
/// وأمره ماكانش بيوصله <b>أبداً</b>.</para>
///
/// <para>🔴 <b>والردود بتتسلسل بـ<c>RackWire.Downstream</c>
/// صراحةً.</b> الراكة بتاخد علامة الوقت من الرد وبتبعتها تاني في
/// <c>?since=</c> — فتاريخ من غير <c>Z</c> معناه زحلقة دايمة في
/// النافذة: يا سحب كل حاجة من تاني كل دورة، يا <b>تخطّي أوامر
/// حقيقية للأبد</b>.</para>
/// </summary>
[ApiController]
[AllowAnonymous]
[RackKey]
[EnableRateLimiting(RackRateLimits.RackApi)]
public sealed class RackFeedsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ⚠️ <b>مفيش أي بيانات اعتماد في الرد ده</b> — لا بصمة ولا ملح
    /// ولا اسم دخول. ده عقد «مين ينفع يتسند» مش عقد دخول.
    /// </summary>
    [HttpGet("api/v2/technicians/repair")]
    [ProducesResponseType<RepairRosterResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Roster(CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        if (Revoked(rack) is { } stop) return stop;

        var result = await sender.Send(new RepairRosterQuery(rack.TenantId), ct);

        return Wire(result.Value);
    }

    /// <summary>
    /// حاويات الاستيراد — <b>عشان تكون متاحة أوفلاين</b>.
    ///
    /// <para>⚠️ <b>والليستة مش سلطة.</b> الفني يقدر يكتب رمز مش
    /// فيها، والسيرفر بيعمل الحاوية وقت المزامنة. لو الليستة بقت
    /// شرط، حاوية وصلت النهاردة كانت هتوقف فحصها لحد ما حد يضيفها
    /// على الموقع.</para>
    /// </summary>
    [HttpGet("api/v2/containers")]
    [ProducesResponseType<ContainersResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Containers(CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        if (Revoked(rack) is { } stop) return stop;

        var result = await sender.Send(new RackContainersQuery(rack.TenantId), ct);

        return Wire(result.Value);
    }

    /// <summary>
    /// أوامر الصيانة المسنودة — <b>سحب تراكمي</b>.
    ///
    /// <para>⚠️ <c>since</c> نص خام بيتحلّل في المعالج، و«مش مفهوم»
    /// بترجع أول صفحة بـ<c>200</c> مش <c>400</c>.</para>
    /// </summary>
    [HttpGet("api/v2/repairs/assigned")]
    [ProducesResponseType<AssignedRepairsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assigned(
        [FromQuery] string? since, CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        if (Revoked(rack) is { } stop) return stop;

        var result = await sender.Send(new AssignedRepairsQuery(rack.TenantId, since), ct);

        return Wire(result.Value);
    }

    /// <summary>
    /// 🔴 <b>الطبقة التانية — ومش قابلة للوصول النهاردة.</b>
    ///
    /// <para>حارس المفتاح بيقبل المحطات <c>Active</c> بس، فالملغية
    /// بتاخد <c>401</c> ومابتوصلش هنا أصلاً. والفرع ده موجود عن قصد
    /// زي القديم بالظبط: <b>لو حد وسّع المصادقة يوم ما</b> (مثلاً
    /// سمح للموقوفة تقرا)، الطبقة التانية بترفض الملغية برضه من غير
    /// ما حد يتفاكر.</para>
    ///
    /// <para>⚠️ والراكة بتحسب <c>401</c> و<c>403</c> نفس الحاجة
    /// («الراكة مش مصرّح لها») — فالفرع ده مالوش تكلفة على
    /// السلك.</para>
    /// </summary>
    private IActionResult? Revoked(Core.Entities.Rack rack) =>
        rack.Status == RackStatus.Revoked
            ? StatusCode(
                StatusCodes.Status403Forbidden,
                new { code = "RackRevoked", message = "الراكة دي اتلغت. اعمل اقتران جديد." })
            : null;

    /// <summary>
    /// 🔴 <b>تسلسل صريح بخيارات التغذيات — مش <c>Ok(...)</c>.</b>
    ///
    /// <para>الخيارات العامة بتتظبّط في <c>Program.cs</c>، وأي تغيير
    /// فيها بكرة بيمشي على سطح الراكة من غير ما حد يلاحظ. التسلسل
    /// الصريح بيخلّي شكل التواريخ على التغذيات دي <b>مكتوب في
    /// الكود</b> مش موروث.</para>
    /// </summary>
    private IActionResult Wire<T>(T body) =>
        new JsonResult(body, RackWire.Downstream);
}
