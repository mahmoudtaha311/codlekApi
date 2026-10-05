using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.ClearOemCodeNames;

/// <inheritdoc cref="ClearOemCodeNamesCommand"/>
public sealed class ClearOemCodeNamesCommandHandler(
    IMaintenanceRepository maintenance,
    ILogger<ClearOemCodeNamesCommandHandler> log)
    : IRequestHandler<ClearOemCodeNamesCommand, Result<OemCleanupResult>>
{
    public async Task<Result<OemCleanupResult>> Handle(
        ClearOemCodeNamesCommand command, CancellationToken cancellationToken)
    {
        int devices = await SweepAsync(
            command,
            maintenance.SystemFamilyDeviceNamesAsync,
            maintenance.ClearDeviceCommercialModelAsync,
            cancellationToken);

        // ⚠️ والفحوص كمان، مش الأجهزة بس — وإلا صفحة الفحص تفضل تعرض
        //    الكود بعد ما صفحة اللاب تتصلّح.
        int reports = await SweepAsync(
            command,
            maintenance.SystemFamilyReportNamesAsync,
            maintenance.ClearReportCommercialModelAsync,
            cancellationToken);

        var result = new OemCleanupResult(devices, reports);

        if (result.Total > 0)
        {
            log.LogInformation(
                "اتشال اسم تجاري مصدره كود مصنّع من {Count} صف ({Devices} جهاز، {Reports} فحص)",
                result.Total, devices, reports);
        }

        return Result.Success(result);
    }

    private static async Task<int> SweepAsync(
        ClearOemCodeNamesCommand command,
        Func<Guid, Guid, int, CancellationToken, Task<IReadOnlyList<CommercialNameRow>>> page,
        Func<Guid, IReadOnlyCollection<Guid>, CancellationToken, Task<int>> clear,
        CancellationToken ct)
    {
        int cleared = 0;
        var after = Guid.Empty;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await page(command.TenantId, after, command.BatchSize, ct);

            if (batch.Count == 0) break;

            after = batch[^1].Id;

            var doomed = batch
                .Where(r => DeviceNaming.StartsWithOemCode(r.CommercialModelName))
                .Select(r => r.Id)
                .ToList();

            if (doomed.Count > 0) cleared += await clear(command.TenantId, doomed, ct);
        }

        return cleared;
    }
}
