using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using MediatR;

namespace Codlek.Application.Features.Containers.CreateContainer;

public sealed record CreateContainerCommand(string Code, string Name, int SortOrder)
    : IRequest<Result<ContainerListItem>>;
