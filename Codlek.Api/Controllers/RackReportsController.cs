using Codlek.Api.Racks;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Sync;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>POST /api/sync/reports</c> — استقبال الفحوص من المحطة.
///
/// <para>🔴 <b>والرد هنا <u>مافيهوش</u> مصفوفة <c>results</c> — عن
/// قصد.</b> قارئ طابور الراكة بيدوّر على <c>results[*].outboxId</c>؛
/// لو لقى الشكل ده من غير صفّه، بيقرا «اترفع» <b>وبيمسح الصف</b>.
/// فاللي محتاج نتيجة لكل صف بيستعمل <c>/api/v2/sync/batch</c>، والصف
/// اللي بيترفع من هنا بيتقفل على مستوى الحالة بس.</para>
/// </summary>
[ApiController]
[AllowAnonymous]
[RackKey]
public sealed class RackReportsController(
    ISender sender, IRackRepository racks) : ControllerBase
{
    [HttpPost("api/sync/reports")]
    [EnableRateLimiting(RackRateLimits.RackApi)]
    [ProducesResponseType<IngestResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Ingest(CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        /*
          🔴 **الحد ده لازم يتفحص قبل ما نقرا أي بايت.**

          الكود القديم كان بيقرا الجسم كله لنص وبعدين يفكّه لشجرة
          كائنات — يعني الذاكرة بتوصل ٣–٤ أضعاف حجم الطلب. والخدمة
          على استضافة ذاكرتها نص جيجا، فطلب واحد كبير كان يقدر يوقّع
          الموقع **لكل الرواكة** مش لواحدة.
        */
        if (Request.ContentLength is > SyncLimits.MaxBodyBytes) return TooLarge();

        List<LaptopReportPayload> payload;

        try
        {
            payload = await ReportPayloadReader.ReadAsync(
                Request.Body, SyncLimits.MaxBodyBytes, ct);
        }
        catch (BadHttpRequestException)
        {
            // ⚠️ الحزام التاني — الترويسة كدبت في حجمها.
            return TooLarge();
        }
        catch (Exception ex)
        {
            /*
              ⚠️ **الجسم البايظ ٤٠٠ برسالة واضحة.**

              والراكة بتقرا الرسالة دي وبتعرضها للفني؛ ٤٠٠ عندها
              حالة «خطأ في البيانات» — بتوقف الصف للمراجعة
              ومابتمسحوش.
            */
            return BadRequest(new { error = ex.Message });
        }

        var result = await sender.Send(
            new IngestReportsCommand(rack.TenantId, rack.Id, payload), ct);

        var ingested = result.Value;

        /*
          ⚠️ **ولمسة المحطة بعد الاستقبال.**

          `LastSeenAtUtc` هو اللي بيخلّي المالك يعرف إن البنش ده لسه
          حيّ، و`ReportsReceived` عدّاد بيتعرض في قايمة المحطات.
          والاتنين بيتحدّثوا هنا لأن ده المسار الوحيد اللي بيوصل منه
          شغل فعلي.
        */
        await racks.TouchAsync(
            rack.Id, ingested.Added + ingested.Updated, ct);

        return Ok(ingested);
    }

    private IActionResult TooLarge() =>
        StatusCode(
            StatusCodes.Status413PayloadTooLarge,
            new
            {
                error = "الحمولة أكبر من المسموح. قسّم الرفع على دفعات أصغر.",
                maxBytes = SyncLimits.MaxBodyBytes,
            });
}
