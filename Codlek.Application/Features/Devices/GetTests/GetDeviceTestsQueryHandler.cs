using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Features.Reports;
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

        /*
          🔴 **الافتراضي ٢٥ هنا، مش ٤٠ زي باقي القوايم.**

          ودي قيمة صريحة في القديم (`Paging(page, pageSize, 25)`)،
          مش سهو: تاب الفحوص في صفحة اللاب بيتعرض في كارت جمب تابات
          تانية، و٤٠ صف فيه بيطوّل الصفحة لدرجة إن اللي بعده مابيبانش.

          ⚠️ وخط الزمن كمان على ٢٥ — بس هناك الرقم **ثابت** مش
          افتراضي، لأنه داخل في حساب الاستراتيجية نفسها.
        */
        const int DefaultSize = 25;

        var (page, size) = Paging.Clamp(query.Page, query.PageSize, DefaultSize);

        var (rows, total) = await devices.TestsAsync(
            me.TenantId, query.DeviceId, page, size, cancellationToken);

        /*
          ⚠️ **أكواد المحطات بقراية واحدة للصفحة.**

          قراية لكل صف كانت بتبقى N+1 على لاب اتفحص عشرين مرة.
        */
        var racks = await devices.RackCodesAsync(
            me.TenantId, rows.Select(r => r.SourceRackId), cancellationToken);

        // ⚠️ والنسخ كمان قراية واحدة للصفحة — ومن الحمولة الخام بـ
        //    JSON_VALUE جوّه SQL، مش بسحب الحمولة (٢٠ كيلو للفحص).
        var versions = await devices.TestVersionsAsync(
            me.TenantId, rows.Select(r => r.ReportId).ToList(), cancellationToken);

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

            // ⚠️ فحص مالوش صف في النسخ = «غير متاح»، زي القديم.
            ReportVersionText.Display(
                versions.GetValueOrDefault(r.ReportId)?.ApplicationVersion),
            ReportVersionText.Display(
                versions.GetValueOrDefault(r.ReportId)?.TestDefinitionVersion),

            r.NotRunCount,
            r.GeneralNote)).ToList();

        return Result.Success(new PagedResult<DeviceTestItem>(
            Items: items,
            Page: page,
            PageSize: size,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, size)));
    }
}
