using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Devices.LookupDevice;

/// <summary>
/// مسح كود لاب.
///
/// <para>🔴 <b>مطابقة تامة — مفيش <c>LIKE</c> ومفيش تقريب.</b>
/// الماسح بيدّي قيمة واحدة محددة، والمطلوب يا اللاب ده يا «مش
/// موجود». قيمة ممسوحة عمرها ما بتعدّي على مسار بحث حر.</para>
/// </summary>
public sealed class LookupDeviceQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<LookupDeviceQuery, Result<DeviceLookupResult>>
{
    public async Task<Result<DeviceLookupResult>> Handle(
        LookupDeviceQuery query, CancellationToken cancellationToken)
    {
        string? code = DeviceScan.Parse(query.Code);

        /*
          🔴 **سببين مختلفين تحت نفس ٤٠٤.**

          «الشكل غلط» معناها إن اللي اتمسح مش كود لاب خالص؛ «مش
          موجود» معناها إن الشكل صح بس مفيش لاب بالكود ده. ودمجهم
          كان بيخلّي الفني اللي مسح QR بتاع حاجة تانية يفتكر إن
          اللاب مش في النظام.
        */
        if (code is null)
            return Result.Failure<DeviceLookupResult>(DeviceErrors.CodeShape(query.Code));

        /*
          🔴 **التوحيد العربي هنا، مش التقني.**

          عمود `NormalizedValue` في مرساة `CompanyCode` بيتكتب
          بالمطبّع العربي، فالمقارنة لازم تكون بنفسه. و`Parse` فوق
          كبّر الحروف خلاص — وده حمّال، لأن المطبّع العربي مابيكبّرش.
        */
        string normalized = ArabicText.Normalize(code);

        var hits = await devices.ResolveCodeAsync(
            me.TenantId, code, normalized, cancellationToken);

        if (hits.Count == 0)
            return Result.Failure<DeviceLookupResult>(DeviceErrors.CodeNotFound(code));

        var first = hits[0];

        return Result.Success(new DeviceLookupResult(
            first.DeviceId,

            // ⚠️ الكود اللي اتقرا، مش كود الصف — اللي بيمسح بيشوف اللي مسحه.
            code,

            first.IsCurrent,

            // 🔴 أكتر من لاب بنفس الكود = استيكر اتنقل.
            Conflict: hits.Count > 1,

            hits.Select(h => new DeviceCodeMatch(h.DeviceId, h.PublicCode, h.IsCurrent))
                .ToList()));
    }
}
