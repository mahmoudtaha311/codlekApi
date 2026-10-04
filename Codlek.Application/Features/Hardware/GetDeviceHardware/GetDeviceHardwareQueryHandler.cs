using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using Codlek.Application.Features.Hardware.GetReportHardware;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Hardware.GetDeviceHardware;

public sealed class GetDeviceHardwareQueryHandler(
    IHardwareRepository hardware,
    ICurrentUser me)
    : IRequestHandler<GetDeviceHardwareQuery, Result<ReportHardwareResponse>>
{
    public async Task<Result<ReportHardwareResponse>> Handle(
        GetDeviceHardwareQuery query, CancellationToken cancellationToken)
    {
        /*
          ⚠️ **اللاب الأول، وبعدين اللقطة — والرسالتين مختلفتين.**

          «الجهاز مش موجود» و«الجهاز مالوش ولا لقطة» حالتين مختلفتين
          تماماً، والواجهة بتعرض كلام مختلف لكل واحدة. دمجهم في رد
          واحد بيخلّي الفني يفتكر إن اللاب اتمسح.
        */
        if (await hardware.FindDeviceAsync(me.TenantId, query.DeviceId, cancellationToken) is null)
            return Result.Failure<ReportHardwareResponse>(HardwareErrors.DeviceNotFound);

        var header = await hardware.FindLatestSnapshotAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        if (header is null)
            return Result.Failure<ReportHardwareResponse>(HardwareErrors.NoSnapshot);

        /*
          ⚠️ **ومفيش حارس «شغلك انت» هنا.**

          السياسة على النقطة مديرين وفوق، فالفني مابيوصلهاش أصلاً.
          وزيادة الحارس كانت هتبقى كود ميّت.
        */
        return Result.Success(await GetReportHardwareQueryHandler.Build(
            hardware, me.TenantId, header, cancellationToken));
    }
}
