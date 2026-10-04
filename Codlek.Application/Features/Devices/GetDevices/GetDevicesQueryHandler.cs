using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using MediatR;

namespace Codlek.Application.Features.Devices.GetDevices;

/// <summary>
/// قايمة الأجهزة.
///
/// <para>⚠️ <b>كل الترجمة والحسابات هنا، بعد القراية.</b> أي
/// <c>...Text(...)</c> جوّه الإسقاط بيترجم عادي وبيعدّي فحوص
/// الوحدة، وبيرمي على قاعدة حقيقية.</para>
/// </summary>
public sealed class GetDevicesQueryHandler(
    IDeviceRepository devices,
    ICurrentUser me)
    : IRequestHandler<GetDevicesQuery, Result<PagedResult<DeviceListItem>>>
{
    public async Task<Result<PagedResult<DeviceListItem>>> Handle(
        GetDevicesQuery query, CancellationToken cancellationToken)
    {
        var filter = DeviceFilters.Build(
            query.Search, query.Status, query.Confidence, query.Outcome,
            query.Technician, query.Rack, query.From, query.To,
            query.Stage, query.Sort, query.Container,
            query.Flag, query.Handover, query.Location,
            query.Page, query.PageSize);

        var (rows, total) = await devices.ListAsync(
            me.TenantId, filter, cancellationToken);

        /*
          ⚠️ **الأعمدة معرّفات، والواجهة محتاجة أسامي — أربع قرايات
          مجمّعة للصفحة كلها، مش واحدة لكل صف.**

          صفحة فيها ٤٠ لاب كانت بتعمل ١٦٠ استعلام بالشكل التاني.
        */
        var places = await devices.LocationNamesAsync(
            me.TenantId, rows.Select(r => r.CurrentLocationId), cancellationToken);

        var holders = await devices.HolderNamesAsync(
            me.TenantId, rows.Select(r => r.CurrentHolderTechnicianId), cancellationToken);

        var rackCodes = await devices.RackCodesAsync(
            me.TenantId, rows.Select(r => r.Last?.SourceRackId), cancellationToken);

        var floors = await devices.RackLocationsAsync(
            me.TenantId, rows.Select(r => r.Last?.SourceRackId), cancellationToken);

        // ⚠️ وقت واحد للصفحة كلها: لفّة بتقرا `UtcNow` كل مرة بتدّي
        // أعمار مختلفة لنفس الشاشة.
        var now = DateTime.UtcNow;

        var items = rows
            .Select(d => new DeviceListItem(
                Id: d.Id,
                PublicCode: d.PublicCode,
                Manufacturer: d.Manufacturer,

                /*
                  🔴 **الاسم التجاري بيغلب الكود الخام في العرض.**

                  «LENOVO 81FK» رقم مالوش معنى للبايع؛
                  «ideapad 330-15ICH» هو اللي الناس بتعرفه. والخام
                  بيفضل في `RawModel` للتفاصيل الفنية.
                */
                Model: d.CommercialModelName.Length > 0 ? d.CommercialModelName : d.RawModel,

                Status: d.Status.ToString(),

                /*
                  ⚠️ **والمدموج بياخد نص فيه الكود الكانوني** بدل
                  «مدموج» الجافة — عشان اللي بيقرا يعرف يروح فين.
                */
                StatusText: d.Status == DeviceLifecycleStatus.Merged
                            && d.MergedIntoCode is { Length: > 0 } canonical
                    ? $"تم دمجه مع {canonical}"
                    : DeviceLifecycleStatusText.Arabic(d.Status),

                Confidence: d.Confidence.ToString(),
                ConfidenceText: DeviceIdentityConfidenceText.Arabic(d.Confidence),

                TestCount: d.TestCount,
                LastSeenAtUtc: d.LastSeenAtUtc,

                Last: d.Last is null
                    ? null
                    : new LastTestSummary(
                        d.Last.ReportId,
                        d.Last.StartedAtUtc,
                        d.Last.TechnicianId,
                        d.Last.TechnicianName,
                        d.Last.TechnicianCode,
                        Text(rackCodes, d.Last.SourceRackId),
                        new TestCounts(
                            d.Last.PassCount, d.Last.FailCount, d.Last.ErrorCount,
                            d.Last.NotPresentCount, d.Last.SkipCount)),

                MergedIntoCode: d.MergedIntoCode ?? "",
                RawModel: d.RawModel,
                MachineType: d.MachineType,
                ContainerId: d.ContainerId,
                ContainerCode: d.ContainerCode,
                PartChangedAtUtc: d.PartChangedAtUtc,
                PartChangeSummary: d.PartChangeSummary,

                Where: Whereabouts(d, places, holders, floors, now)))
            .ToList();

        return Result.Success(new PagedResult<DeviceListItem>(
            Items: items,
            Page: filter.Page,
            PageSize: filter.PageSize,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, filter.PageSize)));
    }

    /// <summary>
    /// ⚠️ <b>تمريرة على المكان المشترك.</b> صفحة اللاب بتبني نفس
    /// اللوحة بالظبط، ونسختين من القاعدة بيخلّوا الاتنين يختلفوا عن
    /// نفس اللاب — راجع <see cref="DeviceWhereaboutsMapping"/>.
    /// </summary>
    private static DeviceWhereabouts Whereabouts(
        DeviceListRow d,
        IReadOnlyDictionary<Guid, string> places,
        IReadOnlyDictionary<Guid, TechnicianLabel> holders,
        IReadOnlyDictionary<Guid, string> floors,
        DateTime nowUtc) =>
        DeviceWhereaboutsMapping.Build(
            d.Stage,
            d.StageChangedAtUtc,
            d.CurrentLocationId,
            d.CurrentHolderTechnicianId,
            d.Last?.SourceRackId,
            places,
            holders,
            floors,
            nowUtc);

    /// <summary>
    /// ⚠️ المعرّف المش معروف بيبقى خانة فاضية — مش <c>Guid</c> خام.
    ///
    /// <para>⚠️ وفيه نسخة من السطر ده في
    /// <see cref="DeviceWhereaboutsMapping"/> كمان. التكرار هنا
    /// مقبول: ده بحث في قاموس مفيهوش قاعدة، بخلاف قواعد «اللاب فين»
    /// اللي اتلمّت في مكان واحد عشان الصفحة والقايمة ميختلفوش.</para>
    /// </summary>
    private static string Text(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var value) ? value : "";
}
