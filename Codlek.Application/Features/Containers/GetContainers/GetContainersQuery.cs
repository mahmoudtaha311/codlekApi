using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using MediatR;

namespace Codlek.Application.Features.Containers.GetContainers;

public sealed record GetContainersQuery(string? Search)
    : IRequest<Result<IReadOnlyList<ContainerListItem>>>;
