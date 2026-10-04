using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Devices.GetIdentifiers;

/// <summary>
/// مراسي هوية اللاب.
///
/// <para>🔴 <b>والترجمة هنا بعد القراية.</b>
/// <c>DeviceIdentifierKindText.Arabic(...)</c> جوّه إسقاط EF بيترجم
/// عادي وبيعدّي فحوص الوحدة، وبيرمي على قاعدة حقيقية.</para>
/// </summary>
public sealed class GetDeviceIdentifiersQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<GetDeviceIdentifiersQuery, Result<IReadOnlyList<DeviceIdentifierItem>>>
{
    public async Task<Result<IReadOnlyList<DeviceIdentifierItem>>> Handle(
        GetDeviceIdentifiersQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **وجود اللاب بيتفحص الأول.**

          من غيره، معرّف غلط كان بيرجّع قايمة فاضية بـ٢٠٠ — واللي
          بيقرا مايعرفش لو اللاب مالوش مراسي ولا اللاب نفسه مش
          موجود.
        */
        if (await devices.FindDetailAsync(me.TenantId, query.DeviceId, cancellationToken) is null)
            return Result.Failure<IReadOnlyList<DeviceIdentifierItem>>(DeviceErrors.NotFound);

        var rows = await devices.IdentifiersAsync(me.TenantId, query.DeviceId, cancellationToken);

        var items = rows.Select(i => new DeviceIdentifierItem(
            i.Kind.ToString(),
            DeviceIdentifierKindText.Arabic(i.Kind),
            i.RawValue,
            i.Source,

            // ⚠️ اسم القيمة كنص — الواجهة بتلوّن بيه، فهو معرّف.
            i.Confidence.ToString(),

            i.IsActive,
            i.FirstSeenAtUtc,
            i.LastSeenAtUtc)).ToList();

        return Result.Success<IReadOnlyList<DeviceIdentifierItem>>(items);
    }
}
