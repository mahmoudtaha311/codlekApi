using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Containers.GetContainer;

public sealed class GetContainerQueryHandler(
    IContainerRepository containers, ICurrentUser me)
    : IRequestHandler<GetContainerQuery, Result<ContainerDetail>>
{
    public async Task<Result<ContainerDetail>> Handle(
        GetContainerQuery query, CancellationToken cancellationToken)
    {
        var row = await containers.FindAsync(me.TenantId, query.Id, cancellationToken);

        if (row is null) return Result.Failure<ContainerDetail>(ContainerErrors.NotFound);

        var devices = await containers.DevicesAsync(
            me.TenantId, row.Id, cancellationToken);

        return Result.Success(new ContainerDetail(
            row.Id, row.Code, row.Name, row.IsActive,
            row.CreatedByName, row.CreatedAtUtc, devices));
    }
}
