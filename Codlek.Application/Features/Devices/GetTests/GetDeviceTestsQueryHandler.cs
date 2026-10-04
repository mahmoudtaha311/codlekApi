using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using MediatR;

namespace Codlek.Application.Features.Devices.GetTests;

/// <summary>
/// فحوص اللاب — مصفّحة.
/// </summary>
public sealed class GetDeviceTestsQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<GetDeviceTestsQuery, Result<PagedResult<DeviceTestItem>>>
{
    public async Task<Result<PagedResult<DeviceTestItem>>> Handle(
        GetDeviceTestsQuery query, CancellationToken cancellationToken)
    {
        // 🔴 وجود اللاب الأول — صفحة فاضية بـ٢٠٠ مش رد على معرّف غلط.
        if (await devices.FindDetailAsync(me.TenantId, query.DeviceId, cancellationToken) is null)
            return Result.Failure<PagedResult<DeviceTestItem>>(DeviceErrors.NotFound);

        var (page, size) = Paging.Clamp(query.Page, query.PageSize);

        var (rows, total) = await devices.TestsAsync(
            me.TenantId, query.DeviceId, page, size, cancellationToken);

        /*
          ⚠️ **أكواد المحطات بقراية واحدة للصفحة.**

          قراية لكل صف كانت بتبقى N+1 على لاب اتفحص عشرين مرة.
        */
        var racks = await devices.RackCodesAsync(
            me.TenantId, rows.Select(r => r.SourceRackId), cancellationToken);

        var items = rows.Select(r => new DeviceTestItem(
            r.ReportId,
            r.StartedAtUtc,
            r.EndedAtUtc,
            r.DurationMs,
            r.TechnicianId,
            r.TechnicianName,
            r.TechnicianCode,

            // ⚠️ المحطة المش معروفة بتبقى خانة فاضية — مش معرّف خام.
            r.SourceRackId is { } id && racks.TryGetValue(id, out string? code) ? code : "",

            new TestCounts(
                r.PassCount, r.FailCount, r.ErrorCount, r.NotPresentCount, r.SkipCount),

            r.StepCount,
            r.GeneralNote)).ToList();

        return Result.Success(new PagedResult<DeviceTestItem>(
            Items: items,
            Page: page,
            PageSize: size,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, size)));
    }
}
