using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.HydrateCommercialModels;

/// <inheritdoc cref="HydrateCommercialModelsCommand"/>
public sealed class HydrateCommercialModelsCommandHandler(
    IMaintenanceRepository maintenance,
    ILogger<HydrateCommercialModelsCommandHandler> log)
    : IRequestHandler<HydrateCommercialModelsCommand, Result<HydrationSweepResult>>
{
    public async Task<Result<HydrationSweepResult>> Handle(
        HydrateCommercialModelsCommand command, CancellationToken cancellationToken)
    {
        int examined = 0, updated = 0;
        var after = Guid.Empty;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await maintenance.DeviceModelsAsync(
                command.TenantId, after, command.BatchSize, cancellationToken);

            if (batch.Count == 0) break;

            after = batch[^1].Id;
            examined += batch.Count;

            // ⚠️ استعلام أدلة واحد للدفعة كلها — القديم كان بيعمل واحد
            //    لكل جهاز، يعني آلاف الرحلات مع كل إقلاع.
            var evidence = await maintenance.ModelEvidenceAsync(
                command.TenantId, [.. batch.Select(d => d.Id)], cancellationToken);

            foreach (var device in batch)
            {
                if (!evidence.TryGetValue(device.Id, out var rows)) continue;

                var next = CommercialModelHydration.Next(
                    new(device.CommercialModelName, device.CommercialModelSource, device.MachineType),
                    rows);

                if (next is not { } values) continue;

                await maintenance.SetCommercialModelAsync(
                    command.TenantId, device.Id, values.Name, values.Source, values.MachineType,
                    cancellationToken);

                updated++;

                log.LogInformation(
                    "الاسم التجاري: {Code}: [{Before}] ← [{After}] ({Source})",
                    device.PublicCode,
                    string.IsNullOrEmpty(device.CommercialModelName) ? "—" : device.CommercialModelName,
                    values.Name, values.Source);
            }
        }

        if (updated > 0) log.LogInformation("الاسم التجاري اتملى على {Count} جهاز", updated);

        return Result.Success(new HydrationSweepResult(examined, updated));
    }
}
