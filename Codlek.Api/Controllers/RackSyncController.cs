using Codlek.Api.Extensions;
using Codlek.Api.Racks;
using Codlek.Application.Features.Rack.LeaseDeviceCodes;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Codlek.Application.Contracts.Rack;
using Codlek.Core.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v2/sync/*</c> — مزامنة الراكة.
///
/// <para>🔴 <b>والمسارات دي مجمّدة حرفياً.</b> تطبيق الراكة شايلها
/// كنصوص ثابتة جوّه البرنامج المنزّل على الراكات في الميدان، واللي
/// بييجي من الإعدادات هو <b>العنوان الأساسي بس</b>.</para>
///
/// <para>🔴 <b>ومفيش <c>[Authorize]</c> — التحقق بمفتاح
/// المحطة.</b> و<c>[AllowAnonymous]</c> على الكلاس عشان أي سياسة
/// افتراضية جايّة بعدين ماتقفلش المسار: الراكة مالهاش توكن ولا
/// مطالبات، والرفض بسياسة كان هيدّي تحويلة أو
/// <c>ProblemDetails</c> — والاتنين كارثة.</para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v2/sync")]
public sealed class RackSyncController(ISender sender) : ControllerBase
{
    /// <summary>
    /// قدرات السيرفر — <b>الباب اللي الراكة بتسأل منه قبل أي
    /// حاجة</b>.
    ///
    /// <para>⚠️ <b>ومفيش حدّ طلبات عليها في القديم</b> — نداء واحد
    /// رخيص كل دورة مزامنة، والخنق هنا بيقفل الراكة عن معرفة إن
    /// السيرفر بيفهمها.</para>
    /// </summary>
    [HttpGet("capabilities")]
    [RackKey]
    [ProducesResponseType<SyncCapabilities>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Capabilities()
    {
        /*
          ⚠️ **المحطة متحققة ومش مستعملة في الرد.**

          القدرات مش بيانات شركة — هي وصف للسيرفر نفسه. بس الحارس
          موجود لأن النقطة دي بتقول للي بيسأل إيه اللي السيرفر
          بيقبله، ودي معلومة لمحطة مسجّلة مش لأي حد.
        */
        _ = HttpContext.Rack();

        return Ok(new SyncCapabilities
        {
            ProtocolVersion = SyncEntityTypes.ProtocolVersion,

            // 🔴 ترتيب بايت — الراكة بتقارن القايمة بالمتخزّن عندها.
            EntityTypes = SyncEntityTypes.Supported
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList(),

            /*
              ⚠️ **النسخ مقصورة على المدعوم.**

              القاموس ممكن يكون فيه نوع معروف بس مش مدعوم في النسخة
              دي — وإعلان نسخة حمولة لنوع السيرفر مش بيقبله بيخلّي
              الراكة تعمل حمولة وتتعلّق في الطابور.
            */
            PayloadVersions = SyncEntityTypes.PayloadVersions
                .Where(p => SyncEntityTypes.Supported.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),

            Downstream = DownstreamFeeds.All,

            ServerTimeUtc = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// بلوك أكواد أجهزة للراكة.
    ///
    /// <para>🔴 <b>وده اللي بيخلّي الراكة تشتغل أوفلاين.</b> الفني
    /// بيفحص لاب جديد وهو مقطوع عن النت، واللاب محتاج كود فوراً
    /// عشان الليبل يتطبع.</para>
    ///
    /// <para>⚠️ <b>معاملات عنوان بس — مفيش جسم.</b> نفس القديم
    /// بالحرف: الراكة بتبعت <c>POST</c> فاضي بمعاملات في العنوان،
    /// وإضافة <c>[FromBody]</c> هنا بتخلّي الطلب ده يرجّع
    /// <c>400</c>.</para>
    ///
    /// <para>⚠️ <b>و<c>size</c> المش مفهوم بياخد الافتراضي مش
    /// <c>400</c></b> — الراكة في الميدان ومش المفروض تقف عشان
    /// معامل.</para>
    /// </summary>
    [HttpPost("/api/v2/devices/lease")]
    [RackKey]
    [EnableRateLimiting(RackRateLimits.RackApi)]
    [ProducesResponseType<DeviceCodeLeaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Lease(
        [FromQuery] int? size,
        [FromQuery] int? consumedThrough,
        CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        var result = await sender.Send(
            new LeaseDeviceCodesCommand(rack.TenantId, rack.Id, size, consumedThrough), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
