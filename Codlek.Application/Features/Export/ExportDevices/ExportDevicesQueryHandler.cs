using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Enums;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportDevices;

/// <summary>
/// تصدير الأجهزة.
///
/// <para>🔴 <b>الفلتر من <see cref="DeviceFilters"/> — نفس اللي
/// القايمة بتستعمله.</b> الزرار في الواجهة مكتوب فوقه «اللي مفلتر
/// على الشاشة مفلتر في الإكسل»، وفي القديم ماكانش صح: تلات فلاتر
/// (التحذيرات والتسليم والجهة) كانوا ناقصين من نسخة التصدير —
/// فالمدير يفلتر على «مخزن الجاهز» ويصدّر فيطلعله <b>كل</b>
/// الأجهزة.</para>
/// </summary>
public sealed class ExportDevicesQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<ExportDevicesQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportDevicesQuery query, CancellationToken cancellationToken)
    {
        var filter = DeviceFilters.Build(
            query.Search, query.Status, query.Confidence, query.Outcome,
            query.Technician, query.Rack, query.From, query.To,
            query.Stage, query.Sort, query.Container,
            query.Flag, query.Handover, query.Location);

        var rows = await devices.ExportAsync(
            me.TenantId, filter, ExportLimits.MaxRows, cancellationToken);

        var sheet = new Sheet("الأجهزة", ExportColumns.Devices,
            ExportLimits.Capped(rows, d => new object?[]
            {
                d.PublicCode,
                d.Manufacturer,
                DeviceNaming.Model(d.CommercialModelName, d.RawModel),
                d.ContainerCode,
                DeviceLifecycleStatusText.Arabic(d.Status),
                DeviceIdentityConfidenceText.Arabic(d.Confidence),
                DeviceOperationalStageText.Arabic(d.Stage),
                d.LocationName,
                d.TestCount,
                ExportValues.Cairo(d.FirstSeenAtUtc),
                ExportValues.Cairo(d.LastSeenAtUtc),
            }));

        return Result.Success(new ExportWorkbook("الأجهزة", [sheet]));
    }
}
