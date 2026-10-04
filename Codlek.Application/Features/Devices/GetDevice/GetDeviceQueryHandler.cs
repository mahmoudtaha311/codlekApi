using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Devices.GetDevice;

/// <summary>
/// صفحة اللاب.
///
/// <para>🔴 <b>كل الترجمة والحسابات هنا — بعد القراية.</b> أي
/// <c>...Text.Arabic(...)</c> جوّه إسقاط EF بيترجم عادي وبيعدّي
/// فحوص الوحدة، وبيرمي على قاعدة حقيقية.</para>
/// </summary>
public sealed class GetDeviceQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<GetDeviceQuery, Result<DeviceDetail>>
{
    public async Task<Result<DeviceDetail>> Handle(
        GetDeviceQuery query, CancellationToken cancellationToken)
    {
        var device = await devices.FindDetailAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        /*
          ⚠️ **والمدموج بيفتح.** صفحة اللاب هي صفحة تاريخه: اللاب
          اللي اندمج في غيره لازم يفتح ويقول إنه اندمج، مش يرجّع
          ٤٠٤ على صف القايمة لسه مشاورة عليه.
        */
        if (device is null) return Result.Failure<DeviceDetail>(DeviceErrors.NotFound);

        var facts = await devices.DetailFactsAsync(me.TenantId, device, cancellationToken);

        /*
          ⚠️ **تلات قرايات أسماء، كل واحدة بمعرّف واحد.**

          نفس الدوال اللي القايمة بتستعملها — فالصفحة بتقرا الأسماء
          من نفس المكان، ومفيش نسخة تانية من «الاسم المش معروف
          بيبقى إيه».
        */
        var places = await devices.LocationNamesAsync(
            me.TenantId, [device.CurrentLocationId], cancellationToken);

        var holders = await devices.HolderNamesAsync(
            me.TenantId, [device.CurrentHolderTechnicianId], cancellationToken);

        /*
          ⚠️ **أكواد الراكات: بتاعة أول فحص وآخر فحص في قراية
          واحدة.**

          التانية بتيجي من الحقائق (آخر فحص)، والأولى متخزّنة على
          اللاب نفسه — واتنين استعلام لاتنين معرّف ماكانوا هيدّوا
          حاجة زيادة.
        */
        var rackCodes = await devices.RackCodesAsync(
            me.TenantId, [device.FirstSeenByRackId, facts.LatestRackId], cancellationToken);

        var floors = await devices.RackLocationsAsync(
            me.TenantId, [facts.LatestRackId], cancellationToken);

        var detail = new DeviceDetail(
            Id: device.Id,
            PublicCode: device.PublicCode,
            Manufacturer: device.LastKnownManufacturer,

            // 🔴 الاسم التجاري لو موجود، والخام لو مفيش — والخام
            //    بيفضل في `RawModel` تحت.
            Model: DeviceNaming.Model(device.CommercialModelName, device.LastKnownModel),

            // ⚠️ اسم القيمة كنص — الواجهة بتلوّن بيه، فهو معرّف.
            Status: device.Status.ToString(),

            /*
              🔴 **وده مش نص القايمة.**

              القايمة بتقول للمدموج «تم دمجه مع كذا»؛ الصفحة بتقول
              «مدموج» وبس. نقطتين ونصّين مختلفين لنفس القيمة عن
              قصد — ونسخ نسخة القايمة هنا بيغيّر خانة مجمّدة.
            */
            StatusText: DeviceLifecycleStatusText.Arabic(device.Status),

            Confidence: device.Confidence.ToString(),
            ConfidenceText: DeviceIdentityConfidenceText.Arabic(device.Confidence),

            CodeState: device.CodeState,
            CodeStateText: DeviceCodeState.Arabic(device.CodeState),

            IdentityBasis: device.IdentityBasis,

            /*
              🔴 **الفاضي بيبقى قايمة فاضية، مش قايمة فيها نص فاضي.**

              `"".Split('+')` بترجّع عنصر واحد فاضي — والواجهة بترسم
              شارة لكل عنصر، فكانت بترسم شارة فاضية على كل لاب
              مالوش أساس هوية.
            */
            IdentityBasisParts: device.IdentityBasis.Length == 0
                ? []
                : device.IdentityBasis.Split('+', StringSplitOptions.RemoveEmptyEntries),

            FirstSeenAtUtc: device.FirstSeenAtUtc,
            FirstSeenTechnicianCode: device.FirstSeenByTechnicianCode,
            FirstSeenTechnicianName: facts.FirstSeenTechnicianName,
            FirstSeenRackCode: Text(rackCodes, device.FirstSeenByRackId),

            LastSeenAtUtc: device.LastSeenAtUtc,
            LatestTechnicianCode: facts.LatestTechnicianCode,
            LatestTechnicianName: facts.LatestTechnicianName,
            LatestRackCode: Text(rackCodes, facts.LatestRackId),
            LatestReportAtUtc: facts.LatestReportAtUtc,

            // 🔴 الكود العام عارياً — مفيش رابط ومفيش مخطّط ومفيش
            //    معرّف داخلي.
            QrPayload: device.PublicCode,

            ReportCount: facts.ReportCount,
            NoteCount: facts.NoteCount,
            IdentifierCount: facts.IdentifierCount,
            SnapshotCount: facts.SnapshotCount,

            RawModel: device.LastKnownModel,
            MachineType: device.MachineType ?? "",
            CommercialModelSource: device.CommercialModelSource ?? "",

            ContainerId: device.ContainerId,
            ContainerCode: facts.ContainerCode,

            /*
              🔴 **بيتبني دايماً — عمره ما بيبقى <c>null</c>.**

              النوع قابل للفراغ في العقد، بس الشاشة بتحرس
              `{d.where && …}` — فإرجاع `null` بيعدّي من المصرّف
              وبيفضّي اللوحة في صمت. واللوحة دي اتضافت أصلاً لأن
              الصفحة كانت أفقر من القايمة.
            */
            Where: DeviceWhereaboutsMapping.Build(
                device.OperationalStage,
                device.StageChangedAtUtc,
                device.CurrentLocationId,
                device.CurrentHolderTechnicianId,
                facts.LatestRackId,
                places,
                holders,
                floors,
                DateTime.UtcNow));

        return Result.Success(detail);
    }

    private static string Text(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var value) ? value : "";
}
