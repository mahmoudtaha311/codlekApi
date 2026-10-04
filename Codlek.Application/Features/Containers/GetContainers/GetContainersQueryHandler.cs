using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Containers.GetContainers;

public sealed class GetContainersQueryHandler(
    IContainerRepository containers, ICurrentUser me)
    : IRequestHandler<GetContainersQuery, Result<IReadOnlyList<ContainerListItem>>>
{
    public async Task<Result<IReadOnlyList<ContainerListItem>>> Handle(
        GetContainersQuery query, CancellationToken cancellationToken) =>
        Result.Success(await containers.ListAsync(me.TenantId, query.Search, cancellationToken));
}
