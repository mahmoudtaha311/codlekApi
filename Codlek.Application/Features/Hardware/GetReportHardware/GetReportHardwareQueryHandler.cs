using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Hardware.GetReportHardware;

public sealed class GetReportHardwareQueryHandler(
    IHardwareRepository hardware,
    ICurrentUser me)
    : IRequestHandler<GetReportHardwareQuery, Result<ReportHardwareResponse>>
{
    public async Task<Result<ReportHardwareResponse>> Handle(
        GetReportHardwareQuery query, CancellationToken cancellationToken)
    {
        var header = await hardware.FindReportHeaderAsync(
            me.TenantId, query.ReportId, cancellationToken);

        if (header is null)
            return Result.Failure<ReportHardwareResponse>(HardwareErrors.ReportNotFound);

        /*
          🔴 **الحارس ده مقصود هنا بالذات.**

          صفحة الفحص نفسها بترفض تفتح فحص مش بتاع الفني، فمن غير
          السطر ده هو بيوصل للّقطة من الرابط ده — يقرا سيريالات بضاعة
          مش شغله.

          ⚠️ والمقارنة بالكود مش بالمعرّف: كود الفني على الفحص
          **لقطة** اتكتبت وقت الفحص، والفني اللي اتشال لسه كوده
          مكتوب عليها.
        */
        if (!me.IsManagerOrAbove && header.TechnicianCode != me.Code)
            return Result.Failure<ReportHardwareResponse>(HardwareErrors.NotYourReport);

        return Result.Success(await Build(hardware, me.TenantId, header, cancellationToken));
    }

    /// <summary>
    /// ⚠️ نفس البناء بالحرف في نقطة الجهاز — مكتوب مرة واحدة عشان
    /// الشاشتين يعرضوا نفس الحقول.
    /// </summary>
    internal static async Task<ReportHardwareResponse> Build(
        IHardwareRepository hardware, Guid tenantId,
        SnapshotHeaderFacts header, CancellationToken ct)
    {
        var components = await hardware.ComponentsAsync(tenantId, header.ReportId, ct);
        string rackCode = await hardware.RackCodeAsync(tenantId, header.SourceRackId, ct);

        var device = header.DeviceId is { } deviceId
            ? await hardware.FindDeviceAsync(tenantId, deviceId, ct)
            : null;

        return HardwareMapping.Snapshot(
            header,
            HardwareMapping.DeviceCode(device, header.SnapshotDeviceCode),
            rackCode,
            components);
    }
}
