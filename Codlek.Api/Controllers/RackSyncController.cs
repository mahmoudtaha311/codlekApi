using System.Text.Json;
using Codlek.Api.Extensions;
using Codlek.Api.Racks;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Contracts.Wire;
using Codlek.Application.Features.Rack.LeaseDeviceCodes;
using Codlek.Application.Features.Rack.SyncBatches;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Sync;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

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
public sealed class RackSyncController(
    ISender sender,
    IRackRepository racks,
    IOptions<RackServerOptions> server) : ControllerBase
{
    /// <summary>ترويسة «الرد ده متخزّن من قبل» — معلومة، الراكة مابتقراهاش.</summary>
    public const string ReplayHeader = "Idempotent-Replay";

    /// <summary>
    /// دفعة مزامنة — <b>نتيجة لكل صف</b>.
    ///
    /// <para>🔴 <b>أخطر نقطة في النظام.</b> الراكة بتقفل صف طابورها
    /// (وبتمسحه) لو الرد <c>2xx</c> ومالقتش صفّها في <c>results</c>
    /// بحالة <c>Rejected</c>. فالرد هنا بيتسلسل بـ
    /// <see cref="RackWire.Wire"/> بس — مش <c>Ok(...)</c> اللي بياخد
    /// إعدادات الموقع (ومعاها محوّل تواريخ بيغيّر البايتات).</para>
    ///
    /// <para>⚠️ <b>والجسم بيتقرا بالإيد — مش <c>[FromBody]</c>.</b>
    /// تلات أسباب: الحد لازم يتفحص قبل أي بايت (الاستضافة ذاكرتها نص
    /// جيجا)؛ وجسم أكبر من الحد جوّه ربط النموذج كان بيطلع <c>500</c>
    /// من معالج الأخطاء (والراكة بتعيد للأبد) بدل <c>413</c>؛ وفلتر
    /// <c>[ApiController]</c> بيرد <c>400</c> على الجسم البايظ <b>قبل</b>
    /// فلتر المفتاح — فطلب من غير مفتاح كان هياخد <c>400</c> مش
    /// <c>401</c> الفاضي.</para>
    /// </summary>
    [HttpPost("batch")]
    [RackKey]
    [EnableRateLimiting(RackRateLimits.RackApi)]
    [ProducesResponseType<SyncBatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<IActionResult> Batch(CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        /*
          ⚠️ **حارس مش بيتوصله النهارده — ومقصود.** المفتاح بيتحقق على
          المحطات الشغّالة بس، فالملغية بتاخد `401` قبل هنا. الحارس
          منقول من القديم بالحرف عشان لو التحقق اتغيّر يوم ما، المحطة
          الملغية تاخد رسالة بتقول تعمل إيه بدل ما ترفع.
        */
        if (rack.Status == RackStatus.Revoked)
        {
            return Readable(StatusCodes.Status403Forbidden, new
            {
                code = "RackRevoked",
                message = "الراكة دي اتلغت. اعمل اقتران جديد.",
            });
        }

        /*
          ⚠️ **نسخة البرنامج قبل الجسم.** نسخة قديمة أوي بتبعت حمولة
          شكلها مختلف — الرفض برسالة واضحة أحسن من بيانات ناقصة محدش
          ياخد باله منها. والراكة بتعرض الرسالة للفني زي ما هي.
        */
        string clientVersion = Request.Headers[ClientVersions.Header].ToString();

        if (!ClientVersions.IsSupported(clientVersion))
        {
            return Readable(StatusCodes.Status426UpgradeRequired, new
            {
                code = "ClientTooOld",
                minVersion = ClientVersions.Minimum,
                message = $"نسخة البرنامج على الراكة دي قديمة ({clientVersion}). " +
                          $"لازم {ClientVersions.Minimum} على الأقل.",
            });
        }

        if (Request.ContentLength is > SyncLimits.MaxBodyBytes) return TooLarge();

        SyncBatchRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SyncBatchRequest>(
                new LimitedStream(Request.Body, SyncLimits.MaxBodyBytes), RackWire.Wire, ct);
        }
        catch (BadHttpRequestException)
        {
            // ⚠️ الحزام التاني — الترويسة كدبت في حجمها أو الطلب chunked.
            return TooLarge();
        }
        catch (JsonException ex)
        {
            // ⚠️ `400` = «خطأ بيانات» عند الراكة: بتوقف الصف للمراجعة
            //    ومابتمسحوش.
            return BadRequest(new { error = "جسم الدفعة مش JSON صالح: " + ex.Message });
        }

        if (body is null) return BadRequest(new { error = "جسم الدفعة فاضي." });

        var outcome = (await sender.Send(
            new SyncBatchCommand(
                rack.TenantId, rack.Id, rack.RackCode,
                server.Value.OfflineTechnicianValidityDays, body),
            ct)).Value;

        if (outcome.Replayed)
        {
            /*
              ⚠️ **الرد المتخزّن مابيلمسش المحطة.** الطلب اللي كتبه زوّد
              العدّاد خلاص — والقديم كان بيزوّده تاني في حالة السباق،
              فرقم «كام فحص وصل» كان بيتضخّم مع كل إعادة متزامنة.
            */
            Response.Headers[ReplayHeader] = "true";
        }
        else
        {
            // ⚠️ `ReportsApplied` مش `Applied` — الأخير بيعدّ الأجهزة كمان.
            await racks.TouchAsync(
                rack.Id, outcome.Response.Summary.ReportsApplied, clientVersion, ct);
        }

        return new JsonResult(outcome.Response, RackWire.Wire);
    }

    /// <summary>
    /// رد خطأ <b>بعربي مقروء</b> — مش بـ<see cref="RackWire.Wire"/>.
    ///
    /// <para>⚠️ <b>وده فرق مقصود عن القديم.</b> <c>RackWire.Wire</c>
    /// بيهرّب كل حرف عربي لرمز من ست خانات — ومش مشكلة في رد الدفعة
    /// لأن الراكة بتفكّه. بس رد الـ<c>426</c> الراكة بتعرض <b>نصّه
    /// الخام</b> للفني (أول ٢٠٠ حرف)، فالتهريب كان بيوصّله رموز مش
    /// كلام. المنسّق العادي للموقع بيكتب العربي زي ما هو.</para>
    /// </summary>
    private ObjectResult Readable(int status, object body) => StatusCode(status, body);

    private ObjectResult TooLarge() =>
        Readable(StatusCodes.Status413PayloadTooLarge, new
        {
            error = "الحمولة أكبر من المسموح. قسّم الرفع على دفعات أصغر.",
            maxBytes = SyncLimits.MaxBodyBytes,
        });

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
