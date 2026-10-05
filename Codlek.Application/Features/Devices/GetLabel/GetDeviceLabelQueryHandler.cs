using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Devices.GetLabel;

/// <summary>
/// ليبل الـQR بتاع اللاب.
///
/// <para>🔴 <b>اللي جوّه الرمز هو <c>PublicCode</c> وبس.</b> مفيش
/// معرّف داخلي ولا رابط: الليبل بيعيش سنين على اللاب، والرابط بيموت
/// أول ما الاستضافة تتغيّر. والمسح بيترجم الكود بالشركة الداخلة —
/// الكود محلي للشركة، مش فريد على النظام.</para>
/// </summary>
public sealed class GetDeviceLabelQueryHandler(
    IDeviceRepository devices,
    IDeviceLabelRenderer renderer,
    ICurrentUser me)
    : IRequestHandler<GetDeviceLabelQuery, Result<DeviceLabel>>
{
    public async Task<Result<DeviceLabel>> Handle(
        GetDeviceLabelQuery query, CancellationToken cancellationToken)
    {
        // ⚠️ والمدموج بياخد ليبل كودُه — زي القديم: كوده المتقاعد لسه
        //    بيترجم له في المسح.
        var device = await devices.FindDetailAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        if (device is null)
            return Result.Failure<DeviceLabel>(DeviceErrors.NotFound);

        // 🔴 كود فاضي = مفيش رمز. لا رمز لسلسلة فاضية ولا معرّف داخلي
        //    مكانه.
        if (string.IsNullOrWhiteSpace(device.PublicCode))
            return Result.Failure<DeviceLabel>(DeviceErrors.NoPublicCode);

        return Result.Success(new DeviceLabel(
            device.PublicCode, renderer.Svg(device.PublicCode)));
    }
}
