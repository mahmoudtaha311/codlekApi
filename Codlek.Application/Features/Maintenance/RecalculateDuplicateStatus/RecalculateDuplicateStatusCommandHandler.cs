using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;

/// <inheritdoc cref="RecalculateDuplicateStatusCommand"/>
public sealed class RecalculateDuplicateStatusCommandHandler(
    IMaintenanceRepository maintenance,
    ILogger<RecalculateDuplicateStatusCommandHandler> log)
    : IRequestHandler<RecalculateDuplicateStatusCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        RecalculateDuplicateStatusCommand command, CancellationToken cancellationToken)
    {
        // ⚠️ قايمة المشكوك فيهم صغيرة (أجهزة بس، مش مراسي) — بتتحسب مرة
        //    واحدة وبعدين الأجهزة بتتمشي عليها دفعات.
        var suspect = await maintenance.DuplicateSuspectsAsync(command.TenantId, cancellationToken);

        int changed = 0;
        var after = Guid.Empty;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await maintenance.ReviewableDevicesAsync(
                command.TenantId, after, command.BatchSize, cancellationToken);

            if (batch.Count == 0) break;

            after = batch[^1].Id;

            var toSuspect = new List<Guid>();
            var toActive = new List<Guid>();

            foreach (var device in batch)
            {
                var wanted = suspect.Contains(device.Id)
                    ? DeviceLifecycleStatus.DuplicateSuspected
                    : DeviceLifecycleStatus.Active;

                if (device.Status == wanted) continue;

                log.LogInformation(
                    "حالة الجهاز {Device} ({Code}) اتغيّرت من {From} لـ {To} بعد إعادة حساب المراسي",
                    device.Id, device.PublicCode, device.Status, wanted);

                (wanted == DeviceLifecycleStatus.Active ? toActive : toSuspect).Add(device.Id);
            }

            if (toSuspect.Count > 0)
            {
                changed += await maintenance.MoveStatusAsync(
                    command.TenantId, toSuspect,
                    DeviceLifecycleStatus.Active, DeviceLifecycleStatus.DuplicateSuspected,
                    cancellationToken);
            }

            if (toActive.Count > 0)
            {
                changed += await maintenance.MoveStatusAsync(
                    command.TenantId, toActive,
                    DeviceLifecycleStatus.DuplicateSuspected, DeviceLifecycleStatus.Active,
                    cancellationToken);
            }
        }

        if (changed > 0) log.LogInformation("حالة {Count} جهاز اتعادت حسابها", changed);

        return Result.Success(changed);
    }
}
