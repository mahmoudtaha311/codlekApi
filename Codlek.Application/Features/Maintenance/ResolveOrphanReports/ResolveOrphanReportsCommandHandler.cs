using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.ResolveOrphanReports;

/// <inheritdoc cref="ResolveOrphanReportsCommand"/>
public sealed class ResolveOrphanReportsCommandHandler(
    IMaintenanceRepository maintenance,
    IReportIngestRepository anchors,
    ILogger<ResolveOrphanReportsCommandHandler> log)
    : IRequestHandler<ResolveOrphanReportsCommand, Result<OrphanSweepResult>>
{
    public async Task<Result<OrphanSweepResult>> Handle(
        ResolveOrphanReportsCommand command, CancellationToken cancellationToken)
    {
        int scanned = 0, linked = 0, flagged = 0;
        var after = Guid.Empty;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await maintenance.OrphanReportsAsync(
                command.TenantId, after, command.BatchSize, cancellationToken);

            if (batch.Count == 0) break;

            after = batch[^1].Id;

            var links = new Dictionary<Guid, List<Guid>>();
            var toFlag = new List<Guid>();

            foreach (var row in batch)
            {
                scanned++;

                var match = await DeviceAnchorMatch.TryMatchAsync(
                    RawSpecs.From(row.RawJson),
                    (kind, value, token) => anchors.DevicesByIdentifierAsync(
                        command.TenantId, kind, value, token),
                    cancellationToken);

                if (match is { } deviceId)
                {
                    if (!links.TryGetValue(deviceId, out var ids)) links[deviceId] = ids = [];
                    ids.Add(row.Id);
                    linked++;
                    continue;
                }

                /*
                  مفيش دليل كافي. بيفضل متعلّم — **ومبيتربطش** بجهاز لمجرد
                  إنه بيشارك سيريال. المشاركة دي هي المشكلة نفسها مش الحل.

                  ⚠️ واللي متعلّم أصلاً مابيتكتبش تاني — العدّاد بيعدّه زي
                  القديم، بس مفيش كتابة على صف ماتغيّرش.
                */
                flagged++;
                if (!row.NeedsDeviceResolution) toFlag.Add(row.Id);
            }

            foreach (var (deviceId, reportIds) in links)
            {
                await maintenance.LinkReportsAsync(
                    command.TenantId, deviceId, reportIds, cancellationToken);

                log.LogInformation(
                    "{Count} فحص من غير جهاز اترطبوا بالجهاز {Device}", reportIds.Count, deviceId);
            }

            if (toFlag.Count > 0)
                await maintenance.FlagReportsAsync(command.TenantId, toFlag, cancellationToken);
        }

        if (scanned > 0)
        {
            log.LogInformation(
                "فحوصات قديمة: {Scanned} اتفحصوا — {Linked} اترطبوا بأجهزة موجودة و{Flagged} مستنيين مراجعة المدير",
                scanned, linked, flagged);
        }

        return Result.Success(new OrphanSweepResult(scanned, linked, flagged));
    }
}
