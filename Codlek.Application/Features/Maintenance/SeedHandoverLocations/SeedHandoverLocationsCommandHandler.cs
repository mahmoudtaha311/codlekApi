using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Maintenance;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.SeedHandoverLocations;

/// <inheritdoc cref="SeedHandoverLocationsCommand"/>
public sealed class SeedHandoverLocationsCommandHandler(
    IMaintenanceRepository maintenance,
    IUnitOfWork unitOfWork,
    ILogger<SeedHandoverLocationsCommandHandler> log)
    : IRequestHandler<SeedHandoverLocationsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        SeedHandoverLocationsCommand command, CancellationToken cancellationToken)
    {
        var have = await maintenance.LocationCodesAsync(command.TenantId, cancellationToken);

        // ⚠️ من غير حساسية للحالة — زي القديم. «wh-5» اللي حد كتبه
        //    بإيده هو نفس «WH-5»، وصف تاني جنبه كان هيكرّر الجهة.
        var missing = HandoverDestinations.All
            .Where(w => !have.Contains(w.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count == 0) return Result.Success(0);

        foreach (var (code, name, kind, sort) in missing)
        {
            maintenance.AddLocation(new Location
            {
                TenantId = command.TenantId,
                Code = code,
                Name = name,
                Kind = kind,
                SortOrder = sort,
                IsActive = true,
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "اتضافت {Count} جهة تسليم ({Codes}) للشركة {Tenant}",
            missing.Count, string.Join("، ", missing.Select(m => m.Code)), command.TenantId);

        return Result.Success(missing.Count);
    }
}
