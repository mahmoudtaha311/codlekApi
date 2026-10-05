using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Maintenance.GetTenantIds;

/// <inheritdoc cref="GetTenantIdsQuery"/>
public sealed class GetTenantIdsQueryHandler(IMaintenanceRepository maintenance)
    : IRequestHandler<GetTenantIdsQuery, Result<IReadOnlyList<Guid>>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        GetTenantIdsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await maintenance.TenantIdsAsync(cancellationToken));
}
