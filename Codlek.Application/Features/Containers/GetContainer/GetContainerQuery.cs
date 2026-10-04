using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using MediatR;

namespace Codlek.Application.Features.Containers.GetContainer;

public sealed record GetContainerQuery(Guid Id) : IRequest<Result<ContainerDetail>>;
